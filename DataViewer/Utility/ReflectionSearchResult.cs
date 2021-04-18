using DataViewer.Utility.ReflectionTree;
using ModMaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataViewer.Utility {

    public class ResultNode<TNode> where TNode : class {
        public TNode Node;
        public List<ResultNode<TNode>> children = new List<ResultNode<TNode>>();
        public ToggleState ToggleState;
        public bool ShowSiblings;
        public bool isMatch;    // flag indicating that this node is an actual matching node and not a parent of a matching node
        public int Count;

        public ResultNode<TNode> FindOrAddChild(TNode node) {
            var rnode = children.Find(rn => rn.Node == node);
            if (rnode == null) {
                rnode = new ResultNode<TNode> { Node = node };
            }
            return rnode;
        }
        public void AddSearchResult(IEnumerable<TNode> path) {
            Count++;
            var rnode = this;
            foreach (var node in path) {
                rnode = rnode.FindOrAddChild(node);
            }
        }
        public void Clear() {
            Node = null;
            Count = 0;
        }
    }
    public class ReflectionSearchResult : ResultNode<Node> {
        public void AddSearchResult(Node node) {
            if (node == null) return;
            var path = new List<Node>();
            for (var n = node; node != null; node = node.GetParent()) {
                path.Add(node);
            }
            AddSearchResult(path.Reverse<Node>());
        }
    }
}

