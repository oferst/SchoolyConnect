using System;
using System.Collections;
using SchoolyConnect;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

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
            if (Program.flag_useJSONFileAndMonitor)
            {
                string receipt = m.SaveSolution(true);
                Log("Solution sent. Receipt = " + receipt);
            }
            else
            {
                m.printSolution();
            }
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
            
            if (Program.flag_useJSONFileAndMonitor)
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


        public override string CheckSolution(SimpleCSP csp, Hashtable cspSolution)
        {
            //Status("Checking consistency with the hard constraints (as reflected in the current database), and making the list of broken soft constraints...");
            m.uncoveredCourses.Clear();
            string res = "";            
            foreach (Constraint c in csp.Constraints)
            {
                if (c.Weight == 0 || c.IsSatisfied(cspSolution)) continue;
                if (c.Weight == Constraint.HARD_CONSTRAINT_WEIGHT)
                {
                    throw new Exception("Breaking hard constraint?? This can't be good :-(  " + c.ToString());
                }
                else
                {
                    res += (c.NegativeDisplayString == "" ? c.ToString() + "* Warning: negative string not defined for this constraint type *" : c.NegativeDisplayString) + "\r\n";

                    // collect the set of the variables involved in violated constraints. 
                    // since those constraints are nooverlap pairs, ideally we should gind a minimal
                    // vertex cover, so as to not report a minimal #. 
                    // The code below is a very greedy approx.: simply add the first vertex if neither vertices
                    // was already selected. 
                    if (!(c.NegativeDisplayString.Contains("no-overlap"))) continue; // we identify those skipped constraints in a crude way. 
                                                            // hopefully there won't be other VarVarConstraint soft constraints. 

                    List<Variable> l = c.getVars();
                    SortedSet<string> keys = new SortedSet<string>();
                    foreach (Variable v in l)
                    {
                        // removing the d_/h_ 
                        string key = v.Name.Substring(2);
                        keys.Add(key);
                    }
                    Debug.Assert(keys.Count == 2);
                    // For seeing the graph: 
                    //string dot = keys.ElementAt(0) + " -- " + keys.ElementAt(1) + ";";
                    //Log(dot);

                    bool covered = false;
                    foreach (string k in keys)
                        if (m.uncoveredCourses.Contains(k))
                        {
                            covered = true;
                           // Log("covered " + k); // lucky, this edge is already covered. 
                            break;
                        }
                    Log("violated: " + c.NegativeDisplayString);
                    if (!covered) m.uncoveredCourses.Add(keys.First());                    
                }
            }
            Log("# Uncovered courses: " + m.uncoveredCourses.Count);            

                    return res;
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
