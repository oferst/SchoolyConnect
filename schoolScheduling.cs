using System;
using System.Collections;
using SchoolyConnect;
namespace CourseScheduling
{
    

    public class schoolScheduling : Scheduling
    {

        TieSchedModel m;

        override public string Text() { return "שיבוץ קורסים"; }

        override public Tuple<Hashtable, Hashtable> GetVarLists()
        {
            return new Tuple<Hashtable, Hashtable>(m.getCourseDVars, m.getCourseHVars);
        }

        override public void ExportSolution(Hashtable cspSolution)
        {        
        }

        override public string name_for_benchmrks()
        {
            return "school";
        }
        override public SimpleCSP TranslateToCSP() // main function for adding constraints **
        {
            m = new TieSchedModel();
            bool useJSONFileAndMonitor = true;
            if (useJSONFileAndMonitor)
            {
                string fileName = "../../data/in/arlozerov.json";
                m.fromJSON(fileName);
               // m.Monitor();
            }
            else
            {
                m.fromJSON("");
                m.Translate();
                Console.ReadLine();
            }
            return m.Translate();
        }

    }
}
