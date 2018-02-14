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

        public TieSchedModel m;
        
        /*********************  TieLib interface ***********************/

        override public string Text() { return "שיבוץ לבתי ספר"; }

        override public Tuple<Hashtable, Hashtable> GetVarLists()
        {
            return new Tuple<Hashtable, Hashtable>(m.getCourseDVars, m.getCourseHVars);
        }

        override public void ExportSolution(Hashtable cspSolution)
        {
            m.ExportSolution(cspSolution);
            if (Program.flag_mode == Program.mode.JSONFile || Program.flag_mode == Program.mode.ServerLoop)
            {
                m.printSolution();
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
            return m.Translate();
        }


        public override string CheckSolution(SimpleCSP csp, Hashtable cspSolution)
        {
            //Status("Checking consistency with the hard constraints (as reflected in the current database), and making the list of broken soft constraints...");
            Dictionary<string, Tuple<Variable, Variable>> tryFix = new Dictionary<string, Tuple<Variable, Variable>>();
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
                    if (!Program.flag_SoftnoOverlap || !(c.NegativeDisplayString.Contains("no-overlap"))) continue; // we identify those skipped constraints in a crude way. 
                                                            // hopefully there won't be other VarVarConstraint soft constraints. 

                    List<Variable> l = c.getVars();
                    SortedSet<string> keys = new SortedSet<string>();
                    foreach (Variable v in l)
                    {
                        // removing the d_/h_ 
                        string key = v.Name.Substring(2);
                        keys.Add(key);
                    }
                    Debug.Assert(keys.Count == 2); // because we are supposed to get here only on overlaps which we turned into soft.
                    
                    bool covered = false;
                    foreach (string k in keys)
                        if (m.uncoveredCourses.Contains(k))
                        {
                            covered = true;  // lucky, this edge is already covered.                            
                            break;
                        }
                    Log("violated: " + c.NegativeDisplayString);
                    if (!covered)
                    {
                        m.uncoveredCourses.Add(keys.First());
                        // heck. Assumse that the d/h pair we need for later is either at locations 0,1, or 2,3
                        if (l[0].Name.Contains(keys.First())) tryFix[keys.First()] = new Tuple<Variable, Variable>(l[0],l[1]);
                        else tryFix[keys.First()] = new Tuple<Variable, Variable>(l[2], l[3]);
                    }
                }
            }
            Log("# Uncovered courses: " + m.uncoveredCourses.Count);

            // fixSolution(csp, cspSolution, tryFix); needs to be fixed.

            return res;
        }


        void fixSolution(SimpleCSP csp, Hashtable cspSolution, Dictionary<string, Tuple<Variable, Variable>> tryFix)
        {
            
            HashSet<string> solved = new HashSet<string>();
            foreach (string course in m.uncoveredCourses)
            {
                Variable vd = tryFix[course].Item1, vh = tryFix[course].Item2;
                int best_day = -1, best_hour = -1, fine = 100;
                for (int h = 0; h < _ObjectWithTimeTable.MAX_HOUR; ++h)
                    for (int d = 0; d < _ObjectWithTimeTable.MAX_DAY; ++d)
                    {                                                
                        if (vd.Domain.Last < h) continue;
                        cspSolution[vd] = d;                        
                        cspSolution[vh] = h;
                        bool fail = false;
                        int slot_fine = 0;
                        foreach (Constraint c in csp.Constraints)
                        {
                            if (c.Weight == 0 || c.IsSatisfied(cspSolution)) continue;
                            if (c.Weight == Constraint.HARD_CONSTRAINT_WEIGHT)
                            {
                                fail = true;
                                break;
                            }
                            slot_fine += c.Weight;                                                            
                        }
                        if (!fail && (slot_fine < fine))
                        {
                            fine = slot_fine;
                            best_day = d;
                            best_hour = h;
                        }
                    }

                if (best_day >=0) // found a slot for course
                {
                    solved.Add(course);
                    Log("Solved " + course + ":" + best_day + "," + best_hour);                    
                    
                    cspSolution[vd] = best_day;
                    cspSolution[vh] = best_hour;
                }
            }
            m.uncoveredCourses = new HashSet<string>(m.uncoveredCourses.Except(solved));
            Log("After fixing, # of uncovered courses: " + m.uncoveredCourses.Count);

        }


        
    }
}
