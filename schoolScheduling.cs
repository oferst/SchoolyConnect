using System;
using System.Collections;
using SchoolyConnect;
namespace CourseScheduling
{
    

    public class schoolScheduling : Scheduling
    {

        TieSchedModel m;
        
        /*********************  TieLib interface ***********************/

        override public string Text() { return "שיבוץ לבתי ספר"; }

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
            /*  revert our sample data solution back to submit status */
              //ResetStaus(); 
            
            /* Workflow for sequential proccessing */
                // Loop();
      
            m = new TieSchedModel();
            bool useJSONFileAndMonitor = true;
            if (useJSONFileAndMonitor)
            {
                string fileName = @"../../data/in/arlozerov.json";
                m.fromJSONFile(fileName);
            }
            else
            {
                m.fromJSONFile("");                
            }

            return m.Translate();
        }

        /*************************************************************************/ 

        void ResetStaus(string solution_id = "2NkgMZLh9RyaRXbhD")
        {
            Connect con = new Connect();
            con.SetStatus(solution_id, "submit");
        }

        void Loop()
        {
            Connect con = new Connect();
            m = new TieSchedModel();

            /******************* Check for pending requests *******************/
            con.PollRequests();
            Log("-----------------------------");
            Log("Pending Requests:");
            con.requests.ForEach(request =>
            {
                if (request.status == "submit")
                {
                    Console.WriteLine(request.AsString());
                    string jsonString = con.GetRequestData(request.solution_id);
                    m.fromJSONString(jsonString);

                    /* notify server, solution in progress */
                    con.SetStatus(request.solution_id, "solver");

                    Log("Solving....");


                    /* solve! */


                    /* populate solution */
                    m.courses.ForEach(course =>
                    {
                        // SolutionCourse c = solution.courses.Find(x=>x.Id==course.Id);
                        // course.ttDay  = SolutionCourse.day;
                        // course.ttHour = SolutionCourse.hour;
                    });
                    m.clusters.ForEach(cluster =>
                    {
                        // SolutionCluster c = Solution.clusters.Find(x=>x.Id==cluster.Id);
                        cluster.Courses.ForEach(course =>
                        {
                            // course.ttDay  = SolutionCourse.day;
                            // course.ttHour = SolutionCourse.hour;
                        });
                    });

                    /*  send solution to Host  */
                    bool isFinal = true;
                    string receipt = m.SaveSolution(isFinal);

                    Log("Solution sent:" + receipt);
                }
            });
            Log("-----------------------------");
            /************************************************************/
        }

    }
}
