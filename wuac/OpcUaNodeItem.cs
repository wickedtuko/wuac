using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wuac
{
    public class OpcUaNodeItem
    {
        public OpcUaNodeItem()
        {
            this.Items = new ObservableCollection<OpcUaNodeItem>();
        }
        public string Title { get; set; }
        public ObservableCollection<OpcUaNodeItem> Items { get; set; }
    }
}
