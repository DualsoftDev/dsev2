using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace PLC.Convert.LSCore.Expression
{
    /// <summary>
    /// Utility class for working with expressions.
    /// </summary>
    public static class ExpressionUtil
    {
        /// <summary>
        /// Recursively retrieves terminals from a given terminal.
        /// </summary>
        /// <param name="t">The terminal to start from.</param>
        /// <param name="findDepthLevel">The maximum depth level to search.</param>
        /// <returns>List of terminals retrieved recursively.</returns>
        public static List<Terminal> GetTerminalsRecursive(Terminal t, int findDepthLevel)
        {
            HashSet<Terminal> addedTerminals = new HashSet<Terminal>();
            Stack<(Terminal Terminal, int Depth)> terminalStack = new Stack<(Terminal, int)>();
            terminalStack.Push((t, 0));

            while (terminalStack.Count > 0)
            {
                var (currentTerm, currentDepth) = terminalStack.Pop();

                if (!addedTerminals.Contains(currentTerm))
                {
                    addedTerminals.Add(currentTerm);

                    if (currentDepth < findDepthLevel && currentTerm.HasInnerExpr)
                    {
                        List<Terminal> innerTerminals = currentTerm.InnerExpr.GetTerminals().ToList();
                        innerTerminals.Reverse(); // Reverse the inner terminals to maintain the order.
                        foreach (Terminal innerTerm in innerTerminals)
                        {
                            terminalStack.Push((innerTerm, currentDepth + 1));
                        }
                    }
                }
            }

            return addedTerminals.ToList();
        }
    }
}
