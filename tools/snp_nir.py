"""Turns a hardware-profile SN P network, as `snp-evolution export-nir` writes it, into a NIR graph, and co-simulates it.

    python tools/snp_nir.py write  part.nir.json part.nir    # writes the NIR file (HDF5)
    python tools/snp_nir.py check  part.nir.json part.nir    # runs the NIR file in norse on every case and compares spikes

The mapping, which is exact for networks within the profile (see HardwareProfile.cs):
- Every neuron becomes one integrate-and-fire (nir.IF) neuron with r = 1, v_reset = 0 and v_threshold = k - 0.5, where
  its rule is a^{>=k} / a^* -> a. NIR fires when v > v_threshold, so integer counts fire exactly from k up. A neuron
  without rules never fires (v_threshold = inf).
- Synapses carry weight 1 from a firing neuron and 0 from a forgetting one, which empties itself but sends nothing.
- Time is discrete, and one SN P step is one time step, dt = 1. NIR's own time is continuous; the step is fixed by
  this choice. A spike sent on step t reaches its targets in time for step t + 1. That is the one-step latency every
  discrete NIR backend gives a recurrent connection, so all synapses are the recurrent edge neurons -> w_rec -> neurons.
- An axonal delay of d steps is a nir.Delay of d time steps on the neuron's output, before w_rec, so its spikes arrive
  on step t + 1 + d. The node is left out when no neuron has a delay.
- The environment's spikes enter through nir.Input and w_in, which a feedforward edge applies on the same time step.
  A spike the SN P system receives on step t is therefore presented at time step t + 1.
Under this mapping the IF neuron fires on time step t exactly when the SN P neuron applies its rule on step t, and its
membrane potential after the step is what the SN P neuron holds after applying its rule, before delivery.
"""

import json
import sys
import warnings

import numpy as np

# torch.jit, which nirtorch still uses, warns that it is deprecated on every load.
warnings.filterwarnings("ignore", category=FutureWarning)


def read_description(path):
    with open(path) as file:
        description = json.load(file)
    if description.get("format") != "snp-nir/1":
        raise SystemExit(f"{path} is not an snp-nir/1 description.")
    return description


def build_graph(description):
    import nir

    count = description["neurons"]
    inputs = description["inputs"]
    threshold = np.array([np.inf if k is None else k - 0.5 for k in description["thresholds"]], dtype=np.float32)
    w_in = np.zeros((count, max(1, len(inputs))), dtype=np.float32)
    for column, port in enumerate(inputs):
        w_in[port["neuron"] - 1, column] = 1
    w_rec = np.zeros((count, count), dtype=np.float32)
    for source, target in description["synapses"]:
        if description["fires"][source - 1]:
            w_rec[target - 1, source - 1] += 1
    delays = np.array(description["delays"], dtype=np.float32)
    nodes = {
        "input": nir.Input(input_type={"input": np.array([w_in.shape[1]])}),
        "w_in": nir.Linear(weight=w_in),
        "neurons": nir.IF(r=np.ones(count, dtype=np.float32), v_threshold=threshold, v_reset=np.zeros(count, dtype=np.float32)),
        "w_rec": nir.Linear(weight=w_rec),
        "output": nir.Output(output_type={"output": np.array([count])}),
    }
    edges = [("input", "w_in"), ("w_in", "neurons"), ("neurons", "output"), ("w_rec", "neurons")]
    if delays.any():
        nodes["delay"] = nir.Delay(delay=delays)
        edges += [("neurons", "delay"), ("delay", "w_rec")]
    else:
        edges += [("neurons", "w_rec")]
    # NIR's Output carries every neuron, so the metadata says which of them are the part's ports.
    metadata = {
        "source": "SNP_Evolution",
        "name": description["name"],
        "time_step": 1.0,
        "inputs": [port["name"] for port in inputs],
        "outputs": {port["name"]: port["neuron"] for port in description["outputs"]},
    }
    return nir.NIRGraph(nodes=nodes, edges=edges, metadata=metadata)


def write(description_path, nir_path):
    import nir

    nir.write(nir_path, build_graph(read_description(description_path)))
    print(f"Wrote {nir_path}.")


class AxonDelay:
    """nir.Delay in whole time steps, which norse does not import itself: a shift register per neuron."""

    @staticmethod
    def module(node):
        import torch

        steps = torch.as_tensor(np.rint(node.delay), dtype=torch.long)

        class Delay(torch.nn.Module):
            def forward(self, x, state=None):
                length = int(steps.max()) + 1
                history = torch.zeros(length, x.shape[-1]) if state is None else state
                history = torch.cat([x.reshape(1, -1), history[:-1]])
                return history[steps, torch.arange(x.shape[-1])], history

        return Delay()


def load_model(nir_path):
    import nir
    import nirtorch
    from norse.torch.utils.import_nir import _import_norse_module

    def model_map(node):
        if isinstance(node, nir.Delay):
            return AxonDelay.module(node)
        return _import_norse_module(node)

    return nirtorch.load(nir.read(nir_path), model_map)


def check(description_path, nir_path):
    import torch

    description = read_description(description_path)
    model = load_model(nir_path)
    inputs = len(description["inputs"])
    failures = 0
    with torch.no_grad():
        for case in description["cases"]:
            state = None
            for step, (applied, held) in enumerate(zip(case["applied"], case["held_after_rule"])):
                # The SN P system receives on step - 1 what NIR integrates on this time step.
                x = torch.zeros(max(1, inputs))
                for column, arrivals in enumerate(case["input"]):
                    x[column] = sum(1 for arrival in arrivals if arrival == step - 1)
                _, state = model(x, state)
                spikes = state.cache["neurons"].reshape(-1)
                fired = sorted(int(index) + 1 for index in torch.nonzero(spikes).flatten())
                potential = state.state["neurons"][0].v.reshape(-1).tolist()
                finite = [round(value) for value in potential]
                if fired != applied or finite != held:
                    print(f"{case['label']}, step {step}: SN P applied rules in {applied} and held {held}; NIR fired {fired} and held {finite}.")
                    failures += 1
                    break
    if failures:
        raise SystemExit(f"{failures} of {len(description['cases'])} case(s) differ.")
    print(f"NIR in norse matches SN P on all {len(description['cases'])} case(s), every step and neuron.")


if __name__ == "__main__":
    if len(sys.argv) != 4 or sys.argv[1] not in ("write", "check"):
        raise SystemExit(__doc__)
    {"write": write, "check": check}[sys.argv[1]](sys.argv[2], sys.argv[3])
