
using SchoolyConnect;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace CourseScheduling
{
    /// <summary>
    /// Class with program entry point.
    /// </summary>
    internal sealed class Program
    {

        public enum mode { JSONFile, Local, ServerLoop}
        static public mode flag_mode = mode.JSONFile;// ServerLoop;// ;

        static public bool flag_ChooseFreeDayForTeachers = false;        
        static public bool flag_SoftnoOverlap = true;
        static public bool flag_filter = true;

        static public int gapWeight = 5; // the weight of a gap

        static void ResetStaus(string solution_id = "rb43wp3XhNjWL3RaY")//"2NkgMZLh9RyaRXbhD")
        {
            Connect con = new Connect();
            con.SetStatus(solution_id, "submit");
        }

        // note that for this to work 
        // 1) for exam scheduling the specialdays table should be correct, and the hard constraints for the exams should be updated. 
        // 2) for class scheduling it has to be after registration info. is in. This happens in late August for winter semester, and around Feb. for spring semester.
        // 3) It limits the 'what-to-schedule'table, and in the end empties it.         
        public const bool make_benchmarks = false; // creates various benchmarks, copies them to the benchmarks dir, and exits.
                
        /// <summary>
        /// Program entry point.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)  // no use for args currently
        {
            GlobalVar.Init(); // initializes the log file. 

            if (flag_mode == mode.ServerLoop)
            {
                ResetStaus(); // temporary                 

                Solver solver = new Solver(solvers.NaPS);
                GlobalVar.SolversTeam.Add(solver);                
                GlobalVar.goalPercent = GlobalVar.initGoalPercent = 1.0f;


                Connect con = new Connect();
                /******************* Check for pending requests *******************/
                con.PollRequests();
                
                con.requests.ForEach(request =>
                {
                    if (request.status == "submit")
                    {
                        schoolScheduling sched = new schoolScheduling();
                        sched.m = new TieSchedModel();
                        sched.Log("-----------------------------");
                        sched.Log("Pending Requests:");
                        sched.Log(request.AsString());
                        string jsonString = con.GetRequestData(request.solution_id);
                        using (StreamWriter file = new StreamWriter("../../data/in/from_server.json"))
                         file.WriteLine(jsonString);
                        sched.m.fromJSONString(jsonString);

                        /* notify server, solution in progress */
                        con.SetStatus(request.solution_id, "solver");

                        sched.Log("Solving....");

                        MainForm m = null;
                        m = new MainForm(sched);
                        m.ShowDialog();                        
                    }
                });
                MessageBox.Show("Ended request Loop");
                GlobalVar.close_log();
                return;
            }



                    if (make_benchmarks)
            {
                MainForm m;
                int[] numSemesters = new int[] { 1, 3, 5, 0 }; // 0 = no limit
                foreach (int i in numSemesters)
                {   
                    // here we need to implement the equivalent of Utils.populate_what_to_schedule(i);
                    string suffix = i == 0? "" : "_" + i.ToString();

                    m = new MainForm(new schoolScheduling());
                    m.createBenchmarks(suffix);                      
                }
                MessageBox.Show("Finished generating benchmarks");
                return;
            }


            
            string title = " "; 
            List<string> schedulers = new List<string> {"לוח שעות"};
            try
            {              
                Config c = new Config(schedulers, title, true);
                if (c.ShowDialog() == DialogResult.Yes)
                {
                    if (GlobalVar.solutiobFile != "") 
                        if (MessageBox.Show("This will read the solution file, and populate the tables. Continue? ", "Import", MessageBoxButtons.OKCancel) == DialogResult.Cancel) return;

                    schoolScheduling sched = new schoolScheduling();
                    sched.m = new TieSchedModel();

                    if (flag_mode == Program.mode.JSONFile)
                    {
                        string fileName = @"../../data/in/from_server.json";// arlozerov.json";
                        sched.m.fromJSONFile(fileName);
                    }
                    else
                    {
                        Debug.Assert(flag_mode == mode.Local);
                        sched.m.fromJSONFile("");
                    }

                    MainForm mf = null;
                    mf = new MainForm(sched); 
                    mf.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                GlobalVar.Log.WriteLine(" *** Program exited on exception:  \n message:" + ex.Message + "\nstacktrace: " + ex.StackTrace + "\nlocation: " + ex.TargetSite);                
            }
            GlobalVar.close_log();
            if (GlobalVar.normal_termination)
            {
                Config c1 = new Config(schedulers,title, false);
                c1.ShowDialog();
            }
        }
    }
}
