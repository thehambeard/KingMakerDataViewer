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
using static ModMaker.Utility.StringExtensions;
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
        private static NodeSearch _shared;
        private static HashSet<int> VisitedInstanceIDs = new HashSet<int> { };
        public static NodeSearch Shared {
            get {
                if (_shared == null) {
                    _shared = new GameObject().AddComponent<NodeSearch>();
                    UnityEngine.Object.DontDestroyOnLoad(_shared.gameObject);
                }
                return _shared;
            }
        }

        private IEnumerator searchCoroutine;
        public static int SequenceNumber = 0;
        public delegate void SearchProgress(int matchCount, int visitCount, int depth, int breadth);
        public void StartSearch(Node node, String searchText, SearchProgress updator) {
            if (searchCoroutine != null) {
                StopCoroutine(searchCoroutine);
                searchCoroutine = null;
            }
            VisitedInstanceIDs.Clear();
            StopAllCoroutines();
            updator(0, 0, 0, 1);
            if (node == null) return;
            node.SetDirty();
            SequenceNumber++;
            Main.Log($"seq: {SequenceNumber} - search for: {searchText}");
            if (searchText.Length == 0) {
//                node.Expanded = ToggleState.On;
            }
            else {
//                node.Expanded = ToggleState.Off;
                searchCoroutine = Search(searchText, new List<Node> { node }, 0, 0, 0, SequenceNumber, updator);
                StartCoroutine(searchCoroutine);
            }
        }
        public void Stop() {
            if (searchCoroutine != null) {
                StopCoroutine(searchCoroutine);
                searchCoroutine = null;
            }
            StopAllCoroutines();
        }
        private IEnumerator Search(String searchText, List<Node> todo, int depth, int matchCount, int visitCount, int sequenceNumber, SearchProgress updator) {
            yield return null;
            if (sequenceNumber != SequenceNumber) yield return null;
            Main.Log(depth, $"seq: {sequenceNumber} depth: {depth} - count: {todo.Count} - todo[0]: {todo.First().Name}");
            var newTodo = new List<Node> { };
            var breadth = todo.Count();
            foreach (var node in todo) {
                bool foundMatch = false;
                var instanceID = node.InstanceID;
                bool alreadyVisted = false;
                if (instanceID is int instID) {
                    if (VisitedInstanceIDs.Contains(instID))
                        alreadyVisted = true;
                    else {
                        VisitedInstanceIDs.Add(instID);
                    }
                }
                if (!alreadyVisted) {
                    visitCount++;
                    node.ChildrenContainingMatches.Clear();
                }
                node.Matches = false;
                Main.Log(depth, $"node: {node.Name}");
                try {
                    if (node.Matches = Matches(node.Name, searchText) || Matches(node.ValueText, searchText)) {
                        Main.Log(depth, $"matched: {node.Name} - {node.ValueText}");
                        foundMatch = true;
                        matchCount++;
                        updator(matchCount, visitCount, depth, breadth);
                        // if we match then mark all parents to root as expanded
                        var parent = node.GetParent();
                        var child = node;
                        var depth2 = depth;
                        while (parent != null) {
                            Main.Log(--depth2, $"< parent {parent.Name} child: {child.Name}");
                            parent.ChildrenContainingMatches.Add(child);
                            child = parent;
                            parent = parent.GetParent();
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
                    if (visitCount % 100 == 0) updator(matchCount, visitCount, depth, breadth);

                }
                try {
                    if (node.hasChildren && !alreadyVisted) {
                        if (node.Name == "SyncRoot") break;
                        if (node.Name == "normalized") break;

                        foreach (var child in node.GetItemNodes()) { 
                            //Main.Log(depth + 1, $"item: {child.Name}"); 
                            newTodo.Add(child); 
                        }
                        foreach (var child in node.GetComponentNodes()) {
                            //Main.Log(depth + 1, $"comp: {child.Name}"); 
                            newTodo.Add(child);
                        }
                        foreach (var child in node.GetPropertyNodes()) {
                            //Main.Log(depth + 1, $"prop: {child.Name}");
                            newTodo.Add(child);
                        }
                        foreach (var child in node.GetFieldNodes()) {
                            //Main.Log(depth + 1, $"field: {child.Name}");
                            newTodo.Add(child);
                        }
                    }
                }
                catch (Exception e) {
                    Main.Log(depth, $"caught - {e}");
                }
                //if (visitCount % 1000 == 0) yield return null;
                if (visitCount % 1000 == 0) {
                    yield return Search(searchText, newTodo, depth , matchCount, visitCount, sequenceNumber, updator);
                    newTodo = new List<Node> { };
                }
            }
            yield return Search(searchText, newTodo, depth + 1, matchCount, visitCount, sequenceNumber, updator);
        }
    }
}
