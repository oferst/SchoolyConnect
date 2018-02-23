using System;
using System.Collections;
using SchoolyConnect;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
                if (Program.flag_LogSolution) m.printSolution();
                if (Program.flag_sendSolution)
                {
                    string receipt = m.SaveSolution(true);
                    Log("Solution sent. Receipt = " + receipt);
                }
            }
            else
            {
                if (Program.flag_LogSolution) m.printSolution();
            }
            cspSolution.Clear();
        }

        override public string name_for_benchmrks()
        {
            return "school";
        }

        


        override public SimpleCSP TranslateToCSP() // main function for adding constraints **
        {
            if (m.Csp.Constraints.Count() > 0)
            {
                Debug.Assert(Program.flag_rerun);
                return m.Csp; // for the rerun
            }
            return m.Translate();
        }


        public override string CheckSolution(SimpleCSP csp, Hashtable cspSolution)
        {
            //Status("Checking consistency with the hard constraints (as reflected in the current database), and making the list of broken soft constraints...");
            Log("# constraints = " + csp.Constraints.Count.ToString());
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
                        string key = keys.First();
                        m.uncoveredCourses.Add(key);
                        Variable vd = l.Find(v1 => v1.Name == ("d_" + key));                        
                        Variable vh = l.Find(v1 => v1.Name == ("h_" + key));
                        tryFix[keys.First()] = new Tuple<Variable, Variable>(vd,vh);                        
                    }
                }
            }
            Log("# Uncovered courses: " + m.uncoveredCourses.Count);

            if (Program.flag_SoftnoOverlap) // todo: also close gaps in other cases
                fixSolution(csp, cspSolution, tryFix); //needs to be fixed.
            if (Program.flag_postProcess) while (fixGaps(csp, cspSolution) > 0);
            if (Program.flag_rerun)
            {
                Log("Rerun: # constraints before = " + csp.Constraints.Count.ToString());
                reRun(csp, cspSolution);                
                m.Csp = csp; // saving it for the next run
                Log("Rerun: # constraints after = " + csp.Constraints.Count.ToString());
            }

            return res;
        }

        /// <summary>
        /// When we run with flag_SoftnoOverlap sometimes we can place those courses that violate the constraints in 
        /// vacant slots that do not violate any hard constraint. 
        /// </summary>
        /// <param name="csp"></param>
        /// <param name="cspSolution"></param>
        /// <param name="tryFix">List of variables to try and fix, compatible with the (string) entries in m.uncoveredCourses </param>
        void fixSolution(SimpleCSP csp, Hashtable cspSolution, Dictionary<string, Tuple<Variable, Variable>> tryFix)
        {
            
            HashSet<string> solved = new HashSet<string>();
            foreach (string course in m.uncoveredCourses)
            {
                Variable vd = tryFix[course].Item1, vh = tryFix[course].Item2;
                int currentDay = (int)cspSolution[vd];
                int currentHour = (int)cspSolution[vh];

                int best_day = -1, best_hour = -1, fine = int.MaxValue;
                for (int h = 0; h < _ObjectWithTimeTable.MAX_HOUR; ++h)
                    for (int d = 0; d < _ObjectWithTimeTable.MAX_DAY; ++d)
                    {                                                
                        if (vh.Domain.Last < h) continue;
                        
                        cspSolution[vd] = d;                        
                        cspSolution[vh] = h;
                        bool fail = false;
                        int slot_fine = 0;
                        foreach (Constraint c in csp.Constraints)
                        {
                            if (c.Weight == 0) continue;                            
                            if (c.IsSatisfied(cspSolution)) continue;
                            if (c.Weight != Constraint.HARD_CONSTRAINT_WEIGHT &&
                                (!Program.flag_SoftnoOverlap || !c.NegativeDisplayString.Contains("no-overlap")))
                            {
                                slot_fine += c.Weight;
                                continue;
                            }
                            if (c.NegativeDisplayString.Contains("gap"))
                            {
                                //a gap constraint can be violated only if we run with flag_softNooverlap=true, 
                                // and while fixing thesolution (in this method), we move a course to a different slot.
                                // by making the move we create a new gap. But this gap is regardless of the current fix (because
                                // we are not goingto report that course in it current schedule anyway). So by continuing
                                // we do not let us stop us from making the move. 
                                continue; // since we only fill slots, gap constraints can only get better anyway.
                            }
                            List<Variable> cVars = c.getVars();
                            bool isInTryFix = false;
                            foreach (Variable v in cVars)
                                if (v != vd && v != vh &&
                                    tryFix.ContainsKey(v.Name.Substring(2)))
                                {
                                    isInTryFix = true;
                                    break;
                                }
                            if (isInTryFix) continue;
                            
                            fail = true;
                            break;
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
                    Log("Solved " + m.getCourseContained(course).Name + ":" + best_day + "," + best_hour);
                    tryFix.Remove(course);
                    cspSolution[vd] = best_day;
                    cspSolution[vh] = best_hour;
                }
                else
                {
                    // bringing back to the way it was
                    cspSolution[vd] = currentDay;
                    cspSolution[vh] = currentHour;
                }
            }
            m.uncoveredCourses = new HashSet<string>(m.uncoveredCourses.Except(solved)); // This is set-minus
            Log("After fixSolution, # of uncovered courses: " + m.uncoveredCourses.Count);
        }
                

        /// <summary>
        /// What course/group is scheduled at day d, hour h for class cl
        /// </summary>
        /// <param name="cspSolution"></param>
        /// <param name="cl"></param>
        /// <param name="d"></param>
        /// <param name="h"></param>
        /// <returns></returns>
        Tuple<Variable, Variable> groupAtHour(Hashtable cspSolution, _Class cl, int d, int h)
        {
            foreach (_Course c in cl.courses)
            {                
                if (c != m.rep(c)) continue;
                
                for (int g = 0; g < c.Hours; ++g)
                {
                    string
                        dv = m.CourseVarName(true, c.Course_Type, c.Id, g),
                        hv = m.CourseVarName(false, c.Course_Type, c.Id, g);
                    if (m.uncoveredCourses.Contains(dv.Substring(2))) continue;  
                    Variable vd = (Variable)m.getCourseDVars[dv];
                    if ((int)cspSolution[vd] != d) continue;
                    Variable vh = (Variable)m.getCourseHVars[hv];
                    if ((int)cspSolution[vh] != h) continue;                  
                    return new Tuple<Variable, Variable>(vd, vh);
                }
            }
            return null;
        }

        /// <summary>
        /// Total sum of weights of broken soft constraints, or -if it breaks a hard constraint. this is 
        /// supposed to happen only because we changed the schedule for trying it. 
        /// </summary>
        /// <param name="csp"></param>
        /// <param name="cspSolution"></param>
        /// <returns></returns>
        int evaluate(SimpleCSP csp, Hashtable cspSolution)
        {   
            int slot_fine = 0;
            foreach (Constraint c in csp.Constraints)
            {
                if (c.Weight == 0 || c.IsSatisfied(cspSolution)) continue;
                if (c.NegativeDisplayString.Contains("gap")) continue; // since we never create gaps (they can only happen because we moved a course that overlapped with something else with flag_softOverlapp=true) then violating it does not matter. 
                if (c.Weight == Constraint.HARD_CONSTRAINT_WEIGHT) return -1;
                
                if (Program.flag_SoftnoOverlap && (c.NegativeDisplayString.Contains("no-overlap")))
                {
                    // check if it involves one of the variables that we are not reporting 
                    List<Variable> list = c.getVars();
                    bool ok = false;
                    foreach (var v in list)
                        if (m.uncoveredCourses.Contains(v.Name.Substring(2)))
                        {
                            ok = true;
                            break;
                        }                    
                    if (!ok) return -1;
                    continue;
                }
                slot_fine += c.Weight;
            }
            return slot_fine;
        }

        /// <summary>
        /// Attempt to fill gaps in the schedule. Very naive: only searches for gaps in early hours, and then 
        /// searches for a filler from late hours, all within the same class.
        /// </summary>
        /// <param name="csp"></param>
        /// <param name="cspSolution"></param>
        int fixGaps(SimpleCSP csp, Hashtable cspSolution)
        {
            int gain = 0;
            foreach (_Class cl in m.classes)
            {
                Log("fix gaps of " + cl.Name);
                bool[] daysCovered_up = new bool[_ObjectWithTimeTable.MAX_DAY];
                for (int h = 1; h < _ObjectWithTimeTable.MAX_HOUR; ++h)
                    for (int d = 0; d < _ObjectWithTimeTable.MAX_DAY; ++d)
                    {
                        if (daysCovered_up[d]) continue;
                        var T = groupAtHour(cspSolution, cl, d, h);
                        if (T != null) continue;

                        // found a gap                                           
                   
                        // We can pay up to Program.gapWeight for closing this gap:
                        int fine = evaluate(csp, cspSolution) + Program.weight_gap;
                   
                        Debug.Assert(fine >= 0); 

                        // Now searching for a filler from the late hours
                        int currentDay, currentHour, best_day = 0;
                        Variable best_day_var = null, best_hour_var = null, best_x_var = null;

                        // we only look for replacements if they do not create a gap. daysCovered[day]=true => the latest course on that day cannot be removed.
                        bool[] daysCovered_down = new bool[_ObjectWithTimeTable.MAX_DAY];
                        for (int hh = _ObjectWithTimeTable.MAX_HOUR - 1; hh > h ; --hh)
                            for (int dd = 0; dd < _ObjectWithTimeTable.MAX_DAY; ++dd)
                            {
                                if (daysCovered_down[dd]) continue;
                                var TT = groupAtHour(cspSolution, cl, dd, hh);
                                if (TT == null) continue;
                                daysCovered_down[dd] = true;
                                currentDay = (int)cspSolution[TT.Item1];
                                currentHour = (int)cspSolution[TT.Item2];
                                Debug.Assert(currentDay == dd && currentHour == hh);

                                // Now fill TT into the gap and evaluate
                                cspSolution[TT.Item1] = d;
                                cspSolution[TT.Item2] = h;
                                // update the x variables accordingly:
                                Variable xv1 = null, xv2 = null;
                                if (Program.flag_gaps_constraints)
                                {
                                    xv1 = m.ClassXVar(cl.Id, d, h);
                                    xv2 = m.ClassXVar(cl.Id, dd, hh);
                                    cspSolution[xv1] = 1;
                                    cspSolution[xv2] = 0;
                                }
                                int res = evaluate(csp, cspSolution);
                                
                                if (res >=0 && res < fine) // found an improvement!
                                {
                                    gain += fine - res;
                                    fine = res;
                                    best_day = dd;                                    
                                    best_day_var = TT.Item1;
                                    best_hour_var = TT.Item2;
                                    if (Program.flag_gaps_constraints) best_x_var = xv2;
                                }
                                
                                cspSolution[TT.Item1] = currentDay;
                                cspSolution[TT.Item2] = currentHour;
                                if (Program.flag_gaps_constraints)
                                {
                                    cspSolution[xv1] = 0;
                                    cspSolution[xv2] = 1;
                                }
                            }
                        if (best_day_var != null)
                        {
                            Log("fixGaps: \n\t" + cl.Name + "\n\t (" + cspSolution[best_day_var].ToString() + "," + cspSolution[best_hour_var].ToString() + ") => (" + d + "," + h + ")");
                            cspSolution[best_day_var] = d;
                            cspSolution[best_hour_var] = h;
                            daysCovered_down[best_day] = false; // this is how we open the option of taking more than one course from the same day.
                            if (Program.flag_gaps_constraints)
                            {
                                Variable xv1 = m.ClassXVar(cl.Id, d, h);
                                cspSolution[xv1] = 1;
                                cspSolution[best_x_var] = 0;
                            }
                        }
                        else daysCovered_up[d] = true;
                    }
            }
            Log("fixGaps reduced fine by " + gain);
            return gain;
        }


        /// <summary>
        /// Here we will add constraints to csp according to the part of cspSolution that we like, and restart the system. 
        /// </summary>
        void reRun(SimpleCSP csp, Hashtable cspSolution)
        {
            foreach(_Class cl in m.classes)
            {
                bool[,] schedule = new bool[_ObjectWithTimeTable.MAX_DAY,_ObjectWithTimeTable.MAX_HOUR];
                foreach (_Course c in cl.courses)
                {
                    if (c != m.rep(c)) continue;
                    for (int g = 0; g < c.Hours; ++g)
                    {

                        Variable vh = (Variable)m.getCourseHVars[m.CourseVarName(false, c.Course_Type, c.Id, g)];
                        int h = (int)cspSolution[vh];
                        // fixing everything up to hour 4.
                        if (h <= 4)
                        {
                            Variable vd = (Variable)m.getCourseDVars[m.CourseVarName(true, c.Course_Type, c.Id, g)];
                            int d = (int)cspSolution[vd];
                            csp.Constraints.Add(new VarValConstraint(vd, d, ArithmeticalOperator.EQ));
                            csp.Constraints.Add(new VarValConstraint(vh, h, ArithmeticalOperator.EQ));
                        }
                        //schedule[d, h] = true;
                    }
                }

                //int minHour = _ObjectWithTimeTable.MAX_DAY;
                //for (int d = 0; d < _ObjectWithTimeTable.MAX_DAY; ++d)
                //    for (int h = _ObjectWithTimeTable.MAX_HOUR - 1; h >=0; --h)
                //    {
                //        if (schedule[d,h])
                //        {
                //            if (h < minHour) minHour = h;
                //            break;
                //        }
                //    }
                //Log("minHour:\n\r" + cl.Name + "\n\t" + minHour);
            }
        }

    }
}
