using ModMaker.Utility;
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
    public partial class NodeSearch : MonoBehaviour {
        private IEnumerator searchCoroutine;
        public static int SequenceNumber = 0;
        public delegate void SearchProgress(int matchCount, int visitCount);
        public void StartSearch(Node node, String searchText, SearchProgress updator ) {
            if (searchCoroutine != null) {
                StopCoroutine(searchCoroutine);
            }
            updator(0, 0);
            if (node == null) return;
            node.SetDirty();
            SequenceNumber++;
            Main.Log($"seq: {SequenceNumber} - search for: {searchText}");
            if (searchText.Length == 0) return;
            searchCoroutine = Search(searchText, new List<Node> { node }, 0, 0, 0, SequenceNumber, updator);
            StartCoroutine(searchCoroutine);
        }
        private IEnumerator Search(String searchText, List<Node> todo, int depth, int matchCount, int visitCount, int sequenceNumber, SearchProgress updator) {
            yield return null;
            if (sequenceNumber != SequenceNumber) yield return null;
            Main.Log(depth, $"depth: {depth} - count: {todo.Count}");
            var newTodo = new List<Node> { };
            foreach (var node in todo) {
                bool foundMatch = false;
                visitCount++;
                try {
                    if (node.Name.Matches(searchText) || node.ValueText.Matches(searchText)) {
                        Main.Log(depth, $"matched: {node.Name} - {node.ValueText}");
                        foundMatch = true;
                        matchCount++;
                        updator(matchCount, visitCount);
                        // if we match then mark all parents to root as expanded
                        var parent = node.GetParent();
                            while (parent != null && !parent.IsDirty() && parent.Expanded != ToggleState.On) {
                                parent.Expanded = ToggleState.On;
                                parent = node.GetParent();
                            }
                    }
                }
                catch (Exception e) {
                    Main.Log(depth, $"caught - {e}");
                }
                if (!foundMatch) {
//                    Main.Log(depth, $"NOT matched: {node.Name} - {node.ValueText}");
                    if (node.Expanded == ToggleState.On && node.GetParent() != null) {
                        node.Expanded = ToggleState.Off;
                    }
                    if (visitCount % 100 == 0) updator(matchCount, visitCount);

                }
                foreach (var child in node.GetItemNodes()) { newTodo.Add(child); }
                foreach (var child in node.GetComponentNodes()) { newTodo.Add(child); }
                foreach (var child in node.GetPropertyNodes()) { newTodo.Add(child); }
                foreach (var child in node.GetFieldNodes()) { newTodo.Add(child); }
                yield return null;
            }
            yield return Search(searchText, newTodo, depth + 1, matchCount, visitCount, sequenceNumber, updator);
        }
    }
}
