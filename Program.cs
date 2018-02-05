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
            /* Pass empty filename for mockup */
            m.fromJSON("");
            m.Solve();
            Console.ReadLine();
        }
    }
}
