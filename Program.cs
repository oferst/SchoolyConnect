using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchoolyConnect
{
    class Program
    {
        static void Main(string[] args)
        {
            TieSchedModel m = new TieSchedModel();
            m.fromJSON("somefile.json");
            m.Solve();
            Console.ReadLine();
        }
    }
}
