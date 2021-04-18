using ModMaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataViewer.Utility {

    public class TreeViewModel<TNode> {
        public TNode Node;
        public ToggleState ToggleState;
    }

    public class TreeSearchResults<TNode> : TreeViewModel<TNode> {
        public List<TreeSearchResults<TNode>> MatchingChildren;
        public bool ShowSiblings;
    }
}
