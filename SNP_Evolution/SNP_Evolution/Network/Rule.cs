using System.Text.RegularExpressions;
using SNP_First_Test.Utilities;

namespace SNP_First_Test.Network
{
    /* Each Rule will contain a set of instructions which will fire a neuron across an axon */
    public class Rule
    {
        // The Regular Expression this rule has to match in order to spike
        public string RuleExpression { get; set; }
        // only used to hold the delay for the given rule, called in neuron
        public int Delay { get; set; }
        // Whether this rule should fire a spike or wipe the spikes currently in the neuron.
        public bool Fire { get; set; }
        public Rule(string ruleExpression, int delay, bool fire)
        {
            RuleExpression = ruleExpression;
            Fire = fire;
            Delay = delay;
        }

        /// <summary>
        /// This method checks whether the provided regex and the provided spike input string, which should be equal to the amount of spikes within the neuron 
        /// If the input rule isn't null, append strict ruling on the rule
        /// If the rule matches directly with the given spikes, return true
        /// otherwise the rule did not match, therefore return false
        /// if the rule was empty, return an error and a false flag.
        /// </summary>
        /// <param name="spikes">rule to check</param>
        /// <returns>True Or False</returns>
        public bool RegexMatch(string spikes)
        {
            if (this.RuleExpression != null)
            {
                Regex rgx = new Regex(Utils.RegexAppendStrict(RuleExpression));
                if (rgx.IsMatch(spikes))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// If a regex match occurs return true as long as the rule should spike
        /// If a regex match does not occur return false
        /// If a regex match occurs but the rule should wipe, return null
        /// </summary>
        /// <param name="currentSpikeAmount">the amount of spikes currently in the network </param>
        /// <returns>Nullable Bool</returns>
        public bool? IsMatched(string currentSpikeAmount)
        {
            if (RegexMatch(currentSpikeAmount) == true)
            {
                if (this.Fire)
                {
                    return true;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return false;
            }
        }
    }
}
