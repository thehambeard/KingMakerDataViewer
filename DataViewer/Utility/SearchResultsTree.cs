using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataViewer.Utility {

    public interface ISearchable {
        bool Matches(String searchText, StringComparison comparison);
    }
    public class SearchGraph<T> where T : ISearchable {


    }
}
