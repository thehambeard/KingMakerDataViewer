using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
using static ModMaker.Utility.ReflectionCache;
using ToggleState = ModMaker.Utility.GUIHelper.ToggleState;

namespace DataViewer.Utility.ReflectionTree {

    /**
     * Strategy For Async Deep Search
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

    public partial class Node {

        public async void SearchAsync(String searchText) {
            IProgress<Node> matched = new Progress<Node>(node => {

            });
            await Task.Run(() => {
                this.Search(searchText, new List<Node> { }, matched);
            });
        }

        private void Search(String searchText, List<Node> todo, IProgress<Node> matched) {

        }
    }
}
