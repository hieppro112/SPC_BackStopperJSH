
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace batchInput_wpf.Helper
{
    public class Aleart_error
    {
        public string nameMachine { get; set; }
        public string idEmployee { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime dateAleart
        {
            get;set;
        } = DateTime.Now;


        
    }
}
