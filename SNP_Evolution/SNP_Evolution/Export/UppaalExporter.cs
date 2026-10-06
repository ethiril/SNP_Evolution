using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;

namespace SnpEvolution.Export
{
    // Model is Uppaal's XML system; Queries is its .q file, one query per line after a comment naming what it checks.
    public sealed record UppaalModel(string Name, string Model, string Queries, IReadOnlyList<string> QueryNames);

    // Our own translation of the engine's step, since the published one (Aman and Ciobanu 2016) could not be read: a
    // Clock broadcasts once a time unit and every neuron takes exactly one edge that changes only its own variables, so
    // the order Uppaal runs the edges in does not matter, and a case is chosen before the first step so every query
    // covers every case.
    public static class UppaalExporter
    {
        // Each query with what it checks, in the order of the .q file.
        public static readonly IReadOnlyList<(string Name, string Query)> ContractQueries = new[]
        {
            ("the run ends on every path", "A<> Clock.End"),
            ("quiet before start", "A[] !quietBroken"),
            ("done once, the right one, with the right outputs", "A[] (Clock.End imply doneOk)"),
            ("back to start", "A[] (Clock.End imply backOk)"),
            ("on time", "A[] (Clock.End imply onTimeOk)"),
        };

        public const string TraceQuery = "A[] !traceBroken";

        // Throws ArgumentException for a rule the translation does not cover (a legacy rule with a delay, which holds the
        // neuron) or a binary out-port.
        public static UppaalModel Export(Part part)
        {
            RefuseUncovered(part);
            Network network = part.Network;
            var task = new ContractTask(part.Contract, part.Binding);
            bool deterministic = SpikeTrace.Choices(network).Count == 0;
            var queries = ContractQueries.ToList();
            if (deterministic)
            {
                queries.Add(("every neuron holds what our engine's run holds, on every step", TraceQuery));
            }
            var model = new StringBuilder();
            model.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            model.Append("<!DOCTYPE nta PUBLIC '-//Uppaal Team//DTD Flat System 1.6//EN' 'http://www.it.uu.se/research/group/darts/uppaal/flat-1_6.dtd'>\n");
            model.Append("<nta>\n");
            model.Append($"<declaration>{Escape(UppaalDeclarations.Of(part, task, deterministic))}</declaration>\n");
            model.Append(ClockTemplate(task.Cases.Count));
            for (int neuron = 0; neuron < network.Neurons.Count; neuron++)
            {
                model.Append(NeuronTemplate(network.Neurons[neuron], neuron));
            }
            string system = "system Clock, " + string.Join(", ", Enumerable.Range(0, network.Neurons.Count).Select(neuron => $"N{neuron + 1}")) + ";";
            model.Append($"<system>{Escape(system)}</system>\n");
            model.Append("</nta>\n");
            string file = string.Concat(queries.Select(query => $"/* {query.Name} */\n{query.Query}\n"));
            return new UppaalModel(PartLibraryFiles.Stem(part.Contract.Name), model.ToString(), file, queries.Select(query => query.Name).ToList());
        }

        private static void RefuseUncovered(Part part)
        {
            if (part.Contract.DataOut.FirstOrDefault(port => port.Kind == PortKind.Binary) is Port binary)
            {
                throw new ArgumentException($"Out-port '{binary.Name}' is binary, which the Uppaal export does not read yet.");
            }
            for (int neuron = 0; neuron < part.Network.Neurons.Count; neuron++)
            {
                if (part.Network.Neurons[neuron].Rules.FirstOrDefault(rule => !rule.IsStandard && rule.Delay > 0 && !rule.Axonal) is Rule held)
                {
                    throw new ArgumentException($"Neuron {neuron + 1} has a legacy rule with a delay ({NetworkNotation.Rule(held)}), which holds the neuron; the Uppaal export covers closing and axonal delays only.");
                }
            }
        }

        // Chooses a case, then ticks once a time unit: release, then deliver, until the run is over.
        private static string ClockTemplate(int cases) =>
            $"""
            <template>
            <name>Clock</name>
            <declaration>clock x;</declaration>
            <location id="c0"><name>Choose</name><committed/></location>
            <location id="c1"><name>Tick</name><label kind="invariant">x &lt;= 1</label></location>
            <location id="c2"><name>Released</name><committed/></location>
            <location id="c3"><name>End</name></location>
            <init ref="c0"/>
            <transition><source ref="c0"/><target ref="c1"/><label kind="select">c : int[0,{cases - 1}]</label><label kind="assignment">caseNo = c, x = 0</label></transition>
            <transition><source ref="c1"/><target ref="c2"/><label kind="guard">x == 1 &amp;&amp; !ended</label><label kind="synchronisation">go!</label><label kind="assignment">x = 0</label></transition>
            <transition><source ref="c2"/><target ref="c1"/><label kind="assignment">deliver()</label></transition>
            <transition><source ref="c1"/><target ref="c3"/><label kind="guard">ended</label></transition>
            </template>

            """;

        // One edge per rule, chosen when it applies; a busy edge while a closing delay runs; an idle edge otherwise.
        private static string NeuronTemplate(Neuron neuron, int id)
        {
            int number = id + 1;
            var text = new StringBuilder();
            text.Append($"<template>\n<name>N{number}</name>\n");
            text.Append($"<location id=\"n{number}\"><name>Run</name></location>\n<init ref=\"n{number}\"/>\n");
            void Edge(string guard, string update) =>
                text.Append($"<transition><source ref=\"n{number}\"/><target ref=\"n{number}\"/><label kind=\"guard\">{Escape(guard)}</label>" +
                    $"<label kind=\"synchronisation\">go?</label><label kind=\"assignment\">{Escape(update)}</label></transition>\n");
            Edge($"closedFor[{id}] > 0", $"busy({id})");
            for (int rule = 0; rule < neuron.Rules.Count; rule++)
            {
                Rule each = neuron.Rules[rule];
                int produce = each.Fire ? (each.IsStandard ? each.Produce : 1) : 0;
                string consume = (each.Consume ?? -1).ToString(CultureInfo.InvariantCulture);
                Edge($"closedFor[{id}] == 0 && applies_{number}_{rule + 1}(spikes[{id}])",
                    $"fireRule({id}, {consume}, {produce}, {each.Delay}, {(each.Axonal ? "true" : "false")})");
            }
            Edge($"closedFor[{id}] == 0 && !anyApplies_{number}(spikes[{id}])", $"idle({id})");
            text.Append("</template>\n");
            return text.ToString();
        }

        private static string Escape(string text) => SecurityElement.Escape(text);
    }
}
