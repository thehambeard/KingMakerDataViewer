using Harmony12;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace ModMaker.Extensions
{
    public static class CodeInstructions
    {
        public static IEnumerable<CodeInstruction> Complete(this IEnumerable<CodeInstruction> codes)
        {
            return codes;
        }

        public static IEnumerable<CodeInstruction> Dump(this IEnumerable<CodeInstruction> codes, Action<string> print)
        {
            print("==== Begin Dumping Instructions ====");
            foreach (CodeInstruction code in codes)
                print($"{code.opcode}, {(code.operand is Label ? ((Label)code.operand).GetHashCode() : code.operand)}, ##{code.labels.FirstOrDefault().GetHashCode()}##");
            print("==== End Dumping Instructions ====");
            return codes;
        }

        public static IEnumerable<CodeInstruction> AddRange(this IEnumerable<CodeInstruction> codes, IEnumerable<CodeInstruction> newCodes)
        {
            return codes.Concat(newCodes);
        }

        public static IEnumerable<CodeInstruction> Insert(this IEnumerable<CodeInstruction> codes, int index, CodeInstruction newCode, bool moveLabelsAtIndex = false)
        {
            return codes.InsertRange(index, new CodeInstruction[] { newCode }, moveLabelsAtIndex);
        }

        public static IEnumerable<CodeInstruction> InsertRange(this IEnumerable<CodeInstruction> codes, int index, IEnumerable<CodeInstruction> newCodes, bool moveLabelsAtIndex = false)
        {
            if (moveLabelsAtIndex)
                codes.MoveLabels(index, newCodes, 0, newCodes.Where(code => code.operand is Label).Select(code => (Label)code.operand));
            return codes.Take(index).Concat(newCodes).Concat(codes.Skip(index));
        }

        public static IEnumerable<CodeInstruction> RemoveRange(this IEnumerable<CodeInstruction> codes, int index, int count, bool moveLabelsAtIndex = false)
        {
            if (moveLabelsAtIndex)
                codes.MoveLabels(index, codes, index + count);
            return codes.Take(index).Concat(codes.Skip(index + count));
        }

        public static IEnumerable<CodeInstruction> Replace(this IEnumerable<CodeInstruction> codes, int index, CodeInstruction newCode, bool moveLabelsAtIndex = false)
        {
            return codes.ReplaceRange(index, 1, new CodeInstruction[] { newCode }, moveLabelsAtIndex);
        }

        public static IEnumerable<CodeInstruction> ReplaceRange(this IEnumerable<CodeInstruction> codes, int index, int count, IEnumerable<CodeInstruction> newCodes, bool moveLabelsAtIndex = false)
        {
            if (moveLabelsAtIndex)
                codes.MoveLabels(index, newCodes, 0, newCodes.Where(code => code.operand is Label).Select(code => (Label)code.operand));
            return codes.Take(index).Concat(newCodes).Concat(codes.Skip(index + count));
        }

        public static CodeInstruction Item(this IEnumerable<CodeInstruction> codes, int index)
        {
            return codes.ElementAt(index);
        }

        public static Label NewLabel(this IEnumerable<CodeInstruction> codes, int index, ILGenerator il)
        {
            Label label = il.DefineLabel();
            codes.MarkLabel(index, label);
            return label;
        }

        public static void MarkLabel(this IEnumerable<CodeInstruction> codes, int index, Label newLabel)
        {
            codes.Item(index).labels.MarkLabel(newLabel);
        }

        public static void MarkLabels(this IEnumerable<CodeInstruction> codes, int index, IEnumerable<Label> newLabels)
        {
            codes.Item(index).labels.MarkLabels(newLabels);
        }

        private static void MarkLabel(this List<Label> labels, Label newLabel)
        {
            labels.Add(newLabel);
        }

        private static void MarkLabels(this List<Label> labels, IEnumerable<Label> newLabel)
        {
            labels.AddRange(newLabel);
        }

        public static void MoveLabels(this IEnumerable<CodeInstruction> codes, int index, IEnumerable<CodeInstruction> targetCodes, int targetIndex)
        {
            List<Label> labels = codes.Item(index).labels;
            targetCodes.MarkLabels(targetIndex, labels);
            labels.Clear();
        }

        public static void MoveLabels(this IEnumerable<CodeInstruction> codes, int index, IEnumerable<CodeInstruction> targetCodes, int targetIndex, IEnumerable<Label> skipLabels)
        {
            List<Label> source = codes.Item(index).labels;
            List<Label> target = targetCodes.Item(targetIndex).labels;
            skipLabels = new HashSet<Label>(skipLabels);
            int i = 0;
            while (i < source.Count)
            {
                if (skipLabels.Contains(source[i]))
                {
                    i++;
                    continue;
                }
                else
                {
                    target.MarkLabel(source[i]);
                    source.RemoveAt(i);
                }
            }
        }

        public static void RemoveLabel(this IEnumerable<CodeInstruction> codes, int index, Label label)
        {
            codes.Item(index).labels.RemoveAll(item => item == label);
        }

        public static void RemoveLabels(this IEnumerable<CodeInstruction> codes, int index, IEnumerable<Label> labels)
        {
            labels = new HashSet<Label>(labels);
            codes.Item(index).labels.RemoveAll(item => labels.Contains(item));
        }

        public static int FindCodes(this IEnumerable<CodeInstruction> codes, IEnumerable<CodeInstruction> findingCodes)
        {
            return codes.FindCodes(findingCodes, new CodeInstructionMatchComparer());
        }

        public static int FindCodes(this IEnumerable<CodeInstruction> codes, int startIndex, IEnumerable<CodeInstruction> findingCodes)
        {
            return codes.FindCodes(startIndex, findingCodes, new CodeInstructionMatchComparer());
        }

        public static int FindCodes(this IEnumerable<CodeInstruction> codes, IEnumerable<CodeInstruction> findingCodes, IEqualityComparer<CodeInstruction> comparer)
        {
            return codes.FindCodes(0, findingCodes, comparer);
        }

        public static int FindCodes(this IEnumerable<CodeInstruction> codes, int startIndex, IEnumerable<CodeInstruction> findingCodes, IEqualityComparer<CodeInstruction> comparer)
        {
            if (findingCodes.Any())
            {
                int ubound = codes.Count() - findingCodes.Count();
                for (int i = startIndex; i <= ubound; i++)
                {
                    if (codes.MatchCodes(i, findingCodes, comparer))
                        return i;
                }
            }
            return -1;
        }

        public static int FindLastCodes(this IEnumerable<CodeInstruction> codes, IEnumerable<CodeInstruction> findingCodes)
        {
            return codes.FindLastCodes(findingCodes, new CodeInstructionMatchComparer());
        }

        public static int FindLastCodes(this IEnumerable<CodeInstruction> codes, IEnumerable<CodeInstruction> findingCodes, IEqualityComparer<CodeInstruction> comparer)
        {
            if (findingCodes.Any())
            {
                int ubound = codes.Count() - findingCodes.Count();
                for (int i = ubound; i >= 0; i--)
                {
                    if (codes.MatchCodes(i, findingCodes, comparer))
                        return i;
                }
            }
            return -1;
        }

        public static bool MatchCodes(this IEnumerable<CodeInstruction> codes, int startIndex, IEnumerable<CodeInstruction> matchingCodes)
        {
            return codes.MatchCodes(startIndex, matchingCodes, new CodeInstructionMatchComparer());
        }

        public static bool MatchCodes(this IEnumerable<CodeInstruction> codes, int startIndex, IEnumerable<CodeInstruction> matchingCodes, IEqualityComparer<CodeInstruction> comparer)
        {
            return codes.Skip(startIndex).Take(matchingCodes.Count()).SequenceEqual(matchingCodes, comparer);
        }

        public class CodeInstructionMatchComparer : IEqualityComparer<CodeInstruction>
        {
            public bool Equals(CodeInstruction x, CodeInstruction y)
            {
                if (y == null)
                    return true;
                else if (x == null)
                    return false;
                else if ((y.opcode == null || OpCodeEquals(y.opcode, x.opcode)) &&
                        (y.operand == null || (y.operand is ValueType ? y.operand.Equals(x.operand) : y.operand == x.operand)) &&
                        (y.labels.Count == 0 || y.labels.TrueForAll(label => x.labels.Contains(label))))
                    return true;
                else
                    return false;
            }

            public int GetHashCode(CodeInstruction obj)
            {
                throw new NotImplementedException();
            }

            private bool OpCodeEquals(OpCode x, OpCode y)
            {
                if (x == OpCodes.Br || x == OpCodes.Br_S)
                    return y == OpCodes.Br || y == OpCodes.Br_S;
                return x == y;
            }
        }
    }
}
