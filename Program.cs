
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

        // general flags
        public enum mode { JSONFile, Local, ServerLoop }
        static public mode flag_mode = mode.JSONFile;// mode.ServerLoop;// ; 
        static public bool flag_sendSolution = true;
        static bool flag_debugConstraints = false;
        static public bool flag_postProcess = true; // attempt to pull late hours into early hours.
        static public bool flag_rerun = false; // attempt to re-run after adding constraints based on the previous solution.
        static public bool flag_LogConstraints = false;
        static public bool flag_LogSolution = false;

        // modeling flags
        static public bool flag_ChooseFreeDayForTeachers = false;
        static public bool flag_SoftnoOverlap = false;
        static public bool flag_filter = true;
        static public bool flag_constrainAllMaxHours = false; // false => only 1-hour-max are constrained. 
        static public bool flag_scheduleTeams = true;
        public enum GapsMode{off, soft, hard };
        static public GapsMode flag_gaps_constraints = GapsMode.soft; //true; // true => add no-gap constraints. 

        // weights
        static public int weight_nooverlap = 8; // when flag_SoftnoOverlap = true, this is the weight
        static public int weight_gap = 5; // Two uses: with flag_gaps_constraints.soft, and when flag_postProcess=true, this is the value of covering an early hour.
        static public int weight_nonHomeTeacherCoursesonFreeDay = 2; // the value of placing non-home-teachers on the home-teacher's free day. 
        static public int weight_homeTeacherOnLateHour = 2;
        static public int weight_hour6 = 1;
        static public int weight_hour7 = 2;
        static public int weight_hour8 = 4;
        static public int weight_hour9 = -1;

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

        public static string fileName = "";
        /// <summary>
        /// Program entry point.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)  // no use for args currently
        {
            GlobalVar.Init(); // initializes the log file. 
            if (flag_debugConstraints) GlobalVar.debug_constraints = true;

            if (flag_mode == mode.ServerLoop)
            {
                //ResetStaus(); // temporary                 

                Solver solver = new Solver(solvers.NaPS);
                GlobalVar.SolversTeam.Add(solver);                
                GlobalVar.goalPercent = GlobalVar.initGoalPercent = 1.0f;


                Connect con = new Connect();
                /******************* Check for pending requests *******************/
                con.PollRequests();

                foreach (_SchoolRequest request in con.requests)
                {
                    if (request.status != "submit") continue;

                    schoolScheduling sched = new schoolScheduling();
                    sched.m = new TieSchedModel();
                    sched.Log("-----------------------------");
                    sched.Log("Pending Requests:");
                    sched.Log(request.AsString());
                    string jsonString = con.GetRequestData(request.solution_id);
                    fileName = "../../data/in/" + request.name + ".json";
                    using (StreamWriter file = new StreamWriter(fileName))
                        file.WriteLine(jsonString);
                    sched.m.fromJSONString(jsonString);

                    /* notify server, solution in progress */
                    con.SetStatus(request.solution_id, "solver");

                    sched.Log("Solving....");

                    MainForm m = null;
                    m = new MainForm(sched);
                    m.ShowDialog();
                };
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
                        fileName = @"../../data/in/עלי גבעה MZ.json";// from_server.json";// ";
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
                    if (flag_rerun)
                    {
                        GlobalVar.solutiobFile = "";
                        mf = new MainForm(sched);
                        mf.ShowDialog();
                    }
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
