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
            m.ExportSolution(cspSolution);
            string receipt = m.SaveSolution(true);
            Log("Solution sent. Receipt = " + receipt);
        }

        override public string name_for_benchmrks()
        {
            return "school";
        }
        override public SimpleCSP TranslateToCSP() // main function for adding constraints **
        {
            m = new TieSchedModel();
            bool useJSONFileAndMonitor = false;
            if (useJSONFileAndMonitor)
            {
                string fileName = "../../data/in/arlozerov.json";
                m.fromJSON(fileName);
            }
            else
            {
                m.fromJSON("");                
            }
            return m.Translate();
        }

    }
}
