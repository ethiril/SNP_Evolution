using System.Linq;
using System.Net;
using System.Text;

namespace SnpEvolution.Model
{
    // Renders a network as a standalone HTML page for exploring in a browser. CSS lays the neurons out in the same
    // columns as NetworkGraph, and a short script draws the synapses over them from the boxes' real positions, using
    // the same routing. Hovering or tapping a neuron highlights the synapses into and out of it.
    public static class NetworkPage
    {
        public static string Html(Network network, string title)
        {
            int[] columns = NetworkGraph.Columns(network);
            var page = new StringBuilder();
            page.AppendLine("<!doctype html>");
            page.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            page.AppendLine($"<title>{Encode(title)}</title>");
            page.AppendLine($"<style>{Style}</style></head><body>");
            page.AppendLine($"<header><h1>{Encode(title)}</h1><p>{network.Neurons.Count} neurons · {network.RuleCount} rules · {network.SynapseCount} synapses · size {network.Size}</p>");
            page.AppendLine("<p class=\"hint\">Hover or tap a neuron to trace its synapses.</p></header>");
            page.AppendLine("<main><div class=\"stage\"><svg class=\"synapses\" aria-hidden=\"true\"><defs>");
            page.AppendLine("<marker id=\"arrow\" viewBox=\"0 0 10 10\" refX=\"10\" refY=\"5\" markerWidth=\"7\" markerHeight=\"7\" orient=\"auto-start-reverse\"><path d=\"M0,0 L10,5 L0,10 z\" class=\"head\"/></marker>");
            page.AppendLine("<marker id=\"arrow-out\" viewBox=\"0 0 10 10\" refX=\"10\" refY=\"5\" markerWidth=\"7\" markerHeight=\"7\" orient=\"auto-start-reverse\"><path d=\"M0,0 L10,5 L0,10 z\" class=\"head-out\"/></marker>");
            page.AppendLine("<marker id=\"arrow-in\" viewBox=\"0 0 10 10\" refX=\"10\" refY=\"5\" markerWidth=\"7\" markerHeight=\"7\" orient=\"auto-start-reverse\"><path d=\"M0,0 L10,5 L0,10 z\" class=\"head-in\"/></marker>");
            page.AppendLine("</defs></svg>");
            for (int column = 0; column <= columns.Max(); column++)
            {
                page.AppendLine("<div class=\"column\">");
                for (int index = 0; index < network.Neurons.Count; index++)
                {
                    if (columns[index] == column)
                    {
                        page.AppendLine(Neuron(network.Neurons[index], index));
                    }
                }
                page.AppendLine("</div>");
            }
            page.AppendLine("</div></main>");
            page.AppendLine($"<script>{Script}</script></body></html>");
            return page.ToString();
        }

        private static string Neuron(Neuron neuron, int index)
        {
            string classes = "neuron" + (neuron.IsInput ? " in" : "") + (neuron.IsOutput ? " out" : "") + (neuron.Module is ModuleTag tag ? $" module m{((tag.Module % 6) + 6) % 6}" : "");
            string targets = string.Join(" ", neuron.Connections);
            var html = new StringBuilder($"<div class=\"{classes}\" data-index=\"{index}\" data-targets=\"{targets}\" tabindex=\"0\">");
            string roles = (neuron.IsInput ? " (in)" : "") + (neuron.IsOutput ? " (out)" : "") + (neuron.Module is ModuleTag module ? $" [m{module.Module}]" : "");
            html.Append($"<div class=\"name\">n{index + 1}{Encode(roles)}</div>");
            if (neuron.InitialSpikes > 0)
            {
                html.Append($"<div class=\"spikes\">{Encode(NetworkNotation.Spikes(neuron.InitialSpikes))}</div>");
            }
            foreach (Rule rule in neuron.Rules)
            {
                html.Append($"<div class=\"rule\">{Encode(NetworkNotation.Rule(rule))}</div>");
            }
            html.Append("</div>");
            return html.ToString();
        }

        private static string Encode(string text) => WebUtility.HtmlEncode(text);

        private const string Style = """
            :root {
              --bg: #ffffff; --text: #1a1a1a; --muted: #6b6b6b; --surface: #fafafa; --border: #3a3a3a;
              --edge: #555555; --edge-dim: #d6d6d6; --out: #c05621; --in: #2b6cb0; --focus: #7c3aed; --incoming: #0d9488;
              --m0: #e6f0ff; --m1: #e9f7ec; --m2: #fff4e0; --m3: #f3e8ff; --m4: #e0f7f7; --m5: #fde8ec;
            }
            @media (prefers-color-scheme: dark) {
              :root:not([data-theme="light"]) {
                --bg: #16171a; --text: #e8e8e8; --muted: #9a9a9a; --surface: #202227; --border: #8a8f98;
                --edge: #a0a4ab; --edge-dim: #34373d; --out: #f0904f; --in: #6aa8f0; --focus: #b18cff; --incoming: #2dd4bf;
                --m0: #1e2a3d; --m1: #1d3125; --m2: #3a2f1a; --m3: #2e2340; --m4: #183535; --m5: #3a1f27;
              }
            }
            :root[data-theme="dark"] {
              --bg: #16171a; --text: #e8e8e8; --muted: #9a9a9a; --surface: #202227; --border: #8a8f98;
              --edge: #a0a4ab; --edge-dim: #34373d; --out: #f0904f; --in: #6aa8f0; --focus: #b18cff; --incoming: #2dd4bf;
              --m0: #1e2a3d; --m1: #1d3125; --m2: #3a2f1a; --m3: #2e2340; --m4: #183535; --m5: #3a1f27;
            }
            * { box-sizing: border-box; }
            body { margin: 0; background: var(--bg); color: var(--text); font: 14px/1.4 system-ui, -apple-system, sans-serif; }
            header { padding: 20px 16px 0; }
            h1 { margin: 0 0 4px; font-size: 18px; }
            header p { margin: 0; color: var(--muted); }
            .hint { font-size: 12px; margin-top: 2px; }
            main { overflow: auto; }
            .stage { position: relative; display: flex; align-items: center; gap: 90px; width: max-content; padding: 90px 110px 50px 80px; }
            .synapses { position: absolute; inset: 0; overflow: visible; pointer-events: none; }
            .synapses path.edge { fill: none; stroke: var(--edge); stroke-width: 1.3; stroke-linejoin: round; marker-end: url(#arrow); transition: stroke .15s, opacity .15s; }
            .head { fill: var(--edge); } .head-out { fill: var(--focus); } .head-in { fill: var(--incoming); }
            .column { display: flex; flex-direction: column; align-items: center; gap: 36px; }
            .neuron {
              position: relative; z-index: 1; min-width: 80px; padding: 8px 14px; text-align: center;
              font: 13px/1.35 ui-monospace, Menlo, Consolas, monospace; white-space: nowrap;
              background: var(--surface); border: 1.3px solid var(--border); border-radius: 14px;
              cursor: pointer; transition: opacity .15s, box-shadow .15s;
            }
            .neuron:focus-visible { outline: 2px solid var(--focus); outline-offset: 3px; }
            .name { font-weight: 700; }
            .neuron.out { border: 2px solid var(--out); }
            .neuron.in { border: 2px solid var(--in); }
            .neuron.out::after, .neuron.in::before { position: absolute; font: 12px system-ui, sans-serif; white-space: nowrap; }
            .neuron.out::after { content: "\2192  env"; left: calc(100% + 8px); top: 70%; transform: translateY(-50%); color: var(--out); }
            .neuron.in::before { content: "in \2192"; right: calc(100% + 8px); top: 30%; transform: translateY(-50%); color: var(--in); }
            .module.m0 { background: var(--m0); } .module.m1 { background: var(--m1); } .module.m2 { background: var(--m2); }
            .module.m3 { background: var(--m3); } .module.m4 { background: var(--m4); } .module.m5 { background: var(--m5); }
            .stage.focus .neuron { opacity: .35; }
            .stage.focus .neuron.active, .stage.focus .neuron.linked { opacity: 1; }
            .stage.focus .neuron.active { box-shadow: 0 0 0 3px var(--focus); }
            .stage.focus path.edge { stroke: var(--edge-dim); }
            .stage.focus path.edge.from { stroke: var(--focus); stroke-width: 2; marker-end: url(#arrow-out); }
            .stage.focus path.edge.to { stroke: var(--incoming); stroke-width: 2; marker-end: url(#arrow-in); }
            """;

        // Mirrors NetworkGraph.Edge: forward synapses curve right edge to left edge, the rest run in staggered lanes
        // beside the columns and over the top so they never cross a neuron.
        private const string Script = """
            (() => {
              const NS = 'http://www.w3.org/2000/svg';
              const stage = document.querySelector('.stage');
              const svg = stage.querySelector('.synapses');
              const neurons = [...stage.querySelectorAll('.neuron')].sort((a, b) => a.dataset.index - b.dataset.index);
              const f = n => Math.round(n * 10) / 10;

              const toward = (from, to, distance) => {
                const length = Math.hypot(to[0] - from[0], to[1] - from[1]);
                const step = length === 0 ? 0 : Math.min(distance, length / 2) / length;
                return [from[0] + (to[0] - from[0]) * step, from[1] + (to[1] - from[1]) * step];
              };
              const rounded = points => {
                let d = `M${f(points[0][0])},${f(points[0][1])}`;
                for (let i = 1; i < points.length - 1; i++) {
                  const before = toward(points[i], points[i - 1], 8), after = toward(points[i], points[i + 1], 8);
                  d += ` L${f(before[0])},${f(before[1])} Q${f(points[i][0])},${f(points[i][1])} ${f(after[0])},${f(after[1])}`;
                }
                const last = points[points.length - 1];
                return d + ` L${f(last[0])},${f(last[1])}`;
              };

              function draw() {
                svg.querySelectorAll('path.edge').forEach(path => path.remove());
                const base = stage.getBoundingClientRect();
                const box = element => {
                  const r = element.getBoundingClientRect();
                  return { x: r.left - base.left, y: r.top - base.top, w: r.width, h: r.height };
                };
                const boxes = neurons.map(neuron => {
                  const b = box(neuron), column = box(neuron.parentElement);
                  return { ...b, right: b.x + b.w, cx: b.x + b.w / 2, cy: b.y + b.h / 2,
                           column: [...stage.children].indexOf(neuron.parentElement), left: column.x, columnRight: column.x + column.w };
                });
                svg.setAttribute('width', stage.scrollWidth);
                svg.setAttribute('height', stage.scrollHeight);
                const top = Math.min(...boxes.map(b => b.y));
                let lane = 0;
                const edge = (a, b) => {
                  if (a === b) {
                    const l = a.cx + 8, r = a.cx + 28, crest = a.y - 28;
                    return `M${f(l)},${f(a.y)} C${f(l)},${f(crest)} ${f(r)},${f(crest)} ${f(r)},${f(a.y)}`;
                  }
                  if (b.column > a.column) {
                    const bend = (b.x - a.right) / 2;
                    return `M${f(a.right)},${f(a.cy)} C${f(a.right + bend)},${f(a.cy)} ${f(b.x - bend)},${f(b.cy)} ${f(b.x)},${f(b.cy)}`;
                  }
                  const spread = 6 * (lane++ % 6);
                  const outward = a.columnRight + 14 + spread;
                  if (b.column === a.column) {
                    return rounded([[a.right, a.cy], [outward, a.cy], [outward, b.cy], [b.right, b.cy]]);
                  }
                  const peak = top - 16 - spread, inward = b.left - 14 - spread;
                  return rounded([[a.right, a.cy], [outward, a.cy], [outward, peak], [inward, peak], [inward, b.cy], [b.x, b.cy]]);
                };
                neurons.forEach((neuron, from) => {
                  neuron.dataset.targets.split(' ').filter(Boolean).map(Number).forEach(position => {
                    const to = position - 1;
                    if (to < 0 || to >= boxes.length) return;
                    const path = document.createElementNS(NS, 'path');
                    path.setAttribute('class', 'edge');
                    path.setAttribute('d', edge(boxes[from], boxes[to]));
                    path.dataset.from = from;
                    path.dataset.to = to;
                    svg.appendChild(path);
                  });
                });
                if (shown !== null) focus(shown);
              }

              let pinned = null, shown = null;
              function focus(index) {
                clear();
                shown = index;
                stage.classList.add('focus');
                neurons[index].classList.add('active');
                svg.querySelectorAll('path.edge').forEach(path => {
                  if (+path.dataset.from === index) { path.classList.add('from'); neurons[path.dataset.to].classList.add('linked'); }
                  if (+path.dataset.to === index) { path.classList.add('to'); neurons[path.dataset.from].classList.add('linked'); }
                  if (path.classList.contains('from') || path.classList.contains('to')) svg.appendChild(path);
                });
              }
              function clear() {
                shown = null;
                stage.classList.remove('focus');
                neurons.forEach(n => n.classList.remove('active', 'linked'));
                svg.querySelectorAll('path.edge').forEach(path => path.classList.remove('from', 'to'));
              }
              neurons.forEach((neuron, index) => {
                neuron.addEventListener('mouseenter', () => { if (pinned === null) focus(index); });
                neuron.addEventListener('mouseleave', () => { if (pinned === null) clear(); });
                neuron.addEventListener('click', event => {
                  event.stopPropagation();
                  pinned = pinned === index ? null : index;
                  pinned === null ? clear() : focus(index);
                });
                neuron.addEventListener('focus', () => { if (pinned === null) focus(index); });
                neuron.addEventListener('blur', () => { if (pinned === null) clear(); });
              });
              document.addEventListener('click', () => { pinned = null; clear(); });

              draw();
              if (document.fonts) document.fonts.ready.then(draw);
              window.addEventListener('resize', draw);
            })();
            """;
    }
}
