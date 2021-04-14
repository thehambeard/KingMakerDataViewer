using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static ModMaker.Utility.ReflectionCache;
using static ModMaker.Utility.RichTextExtensions;
using ToggleState = ModMaker.Utility.ToggleState;

namespace DataViewer.Utility.ReflectionTree {

    /**
     * Strategy For Async Deep Search
     * 
     * --- update
     * 
     * duh can't do real async/Task need to use unity coroutines ala https://docs.unity3d.com/ScriptReference/MonoBehaviour.StartCoroutine.html
     * 
     * two coroutines implemented with Task() and async/await
     *      Render Path - regular OnGUI on the main thread
     *      Search Loop
     *          background thread posting updates using IProgress on the main thread using something like
     *              private async void Button_Click(object sender, EventArgs e) 
     *              here https://stackoverflow.com/questions/12414601/async-await-vs-backgroundworker
     * 
     * Store Node.searchText as a static
     * 
     * Add to node
     *      HashSet<String> matches
     *      searchText
     * 
     * Node.Render(depth) - main thread (UI)
     *      if (!autoExpandKeys.IsEmpty), foreach (key, value) display {key}, {value | Render(children+1) )
     *      if (isExpanded) foreach (key, value) display {key}, {value | Render(children+1) )
      *     yield
     * 
     * Node.Search(string[] keyPath, Func<Node,Bool> matches, int depth) - background thread
     *      autoMatchKeys.Clear()
     *      foreach (key, value) 
     *          if (matches(key) matches += key
     *          if (value.isAtomic && matches(value))  matches += key
     *          if we added any keys to matches then {
     *              foreach parent = Node.parent until Node.IsRoot {
     *                  depth -= 1
     *                  parKey = keyPath[depth]
     *                  if parent.autoMatchKeys.Contains(parKey) done // another branch pupulated through that key
     *                  parent.matches += parKey
     *              }
     *          }
     *          else (value as Node).Search(keyPath + k ey, matches)
     *          
     *          
     * Bool Matches(text)
     *      if (text.contains(searchText) return true
     * 
     * On User click expand for Node, Node.isExpanded = !Node.isExpanded
     *      
     * On searchText change
     *      foreach Node in Tree, this.matches.Clear()
     *      
     */
    public static class ReflectionTreeSearch {
        public static bool Matches(this string source, string other) {
            return source?.IndexOf(other, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        public static string MarkedSubstring(this string source, string other) {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(other)) return source;
            var index = source.IndexOf(other, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) {
                var substr = source.Substring(index, other.Length);
                source = source.Replace(substr, substr.Cyan()).Bold();
            }
            return source;
        }
        public static string Repeat(this string s, int n) => new StringBuilder(s.Length * n).Insert(0, s, n).ToString();
    }
    public partial class Node {
        public static int SequenceNumber = 0;
        public async void SearchAsync(String searchText) {
            this.SetDirty();
            SequenceNumber++;
            Main.Log($"seq: {SequenceNumber} - search for: {searchText}");
            if (searchText.Length == 0) return;
            IProgress<Node> matched = new Progress<Node>(node => {
                while (node != null && !node.IsDirty()) {
                    node.Expanded = ToggleState.On;
                    node = node.GetParent();
                }
            });
            await Task.Run(() => {
                Search(searchText, new List<Node> { this }, matched, SequenceNumber);
            }).ConfigureAwait(false);
        }

        public struct SynchronizationContextAwaiter : INotifyCompletion {
            private static readonly SendOrPostCallback _postCallback = state => ((Action)state)();

            private readonly SynchronizationContext _context;
            public SynchronizationContextAwaiter(SynchronizationContext context) {
                _context = context;
            }

            public bool IsCompleted => _context == SynchronizationContext.Current;

            public void OnCompleted(Action continuation) => _context.Post(_postCallback, continuation);

            public void GetResult() { }
        }
        public static SynchronizationContextAwaiter GetAwaiter(this SynchronizationContext context) {
            return new SynchronizationContextAwaiter(context);
        }

        private static void Search(String searchText, List<Node> todo, IProgress<Node> matched, int sequenceNumber) {

            var syncContext = SynchronizationContext.Current;
            var depth = 0;
            var nodesTodo = todo;
            while (nodesTodo.Count > 0) {
                if (sequenceNumber != SequenceNumber) return;
                Main.Log(" ".Repeat(depth) + $"depth: {depth} - count: {nodesTodo.Count}");
                var newTodo = new List<Node> { };
                foreach (var node in nodesTodo) {
                    Main.Log(" ".Repeat(depth) + $"hi - {node}");
                    bool foundMatch = false;
                    try {
                        Main.Log(" ".Repeat(depth) + $"h2 - {node}");
                        if (node.Name.Matches(searchText) || node.ValueText.Matches(searchText)) {
                            Main.Log(" ".Repeat(depth) + $"matched: {node.Name} - {node.ValueText}");
                            //                            matched.Report(node);
                            foundMatch = true;
                        }
                    }
                    catch (Exception e) {
                        Main.Log(" ".Repeat(depth) + $"caught - {e}");
                    }
                    if (!foundMatch) {
                        Main.Log(" ".Repeat(depth) + $"NOT matched: {node.Name} - {node.ValueText}");

                        if (node.Expanded == ToggleState.On && node.GetParent() != null) {
                            node.Expanded = ToggleState.Off;
                        }
                    }
                    Main.Log(" ".Repeat(depth) + "GetItemNodes");
                    foreach (var child in node.GetItemNodes()) { nodesTodo.Add(child); }
                    Main.Log(" ".Repeat(depth) + "GetComponentNodes");
                    foreach (var child in node.GetComponentNodes()) { newTodo.Add(child); }
                    Main.Log(" ".Repeat(depth) + "GetPropertyNodes");
                    foreach (var child in node.GetPropertyNodes()) { newTodo.Add(child); }
                    Main.Log(" ".Repeat(depth) + "GetFieldNodes");
                    foreach (var child in node.GetFieldNodes()) { newTodo.Add(child); }
                }
                nodesTodo = newTodo;
                depth++;
            }
        }
    }
}
