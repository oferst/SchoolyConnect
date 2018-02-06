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


                /* solve it! */


                /* populate solution */
                bool isFinal = true;
                m.courses.ForEach(course =>
                {
                    // SolutionCourse c = solution.courses.Find(x=>x.Id==course.Id);
                    // course.ttDay  = SolutionCourse.day;
                    // course.ttHour = SolutionCourse.hour;
                });
                m.clusters.ForEach(cluster =>
                {
                    // SolutionCluster c = Solution.clusters.Find(x=>x.Id==cluster.Id);
                    // cluster.ttDay = SolutionCourse.day;
                    // cluster.ttHour = SolutionCourse.hour;
                });
                /* send solution to Host */
                string receipt = m.SaveSolution(isFinal);
                
                Console.WriteLine("Solution sent:" + receipt);

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
