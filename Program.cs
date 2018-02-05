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
            /* display hebrew in the console window 
               if still gibrish change the font on the command window:
               When the console is open, right click the square on top left,
               then properties then font and set hebrew supported font like miriam fixed.
            */
            Console.OutputEncoding = Encoding.UTF8;

            TieSchedModel m = new TieSchedModel();
            /* Pass empty filename for mockup */
            bool useJSONFileAndMonitor = true;
            if (useJSONFileAndMonitor)
            {
                string fileName = "../../data/in/arlozerov.json";
                m.fromJSON(fileName);
                m.Monitor();
            } else
            {
                m.fromJSON("");
                m.Solve();
                Console.ReadLine();
            }
        }
    }
}
