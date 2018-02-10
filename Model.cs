using System;
using System.Collections.Generic;
using System.Linq;
using CourseScheduling;
using System.Collections;
using System.Diagnostics;

namespace SchoolyConnect
{
    class TieSchedCourse : _Course
    {
        public string my_private_prop;        

        public TieSchedCourse(_Course c):base(c)
        {
            my_private_prop = "Created On " + DateTime.Now.ToLongTimeString();
        }
    }

    class TieSchedModel : ModelBase
    {
        List<TieSchedCourse> tCourses;
        Hashtable courseDVars = new Hashtable();
        Hashtable courseHVars = new Hashtable();

        public Hashtable getCourseDVars { get { return courseDVars; } }
        public Hashtable getCourseHVars { get { return courseHVars; } }

        // Here we will fill courses that violate nooverlap constraints (should be hard constrained, but we make them soft).
        public HashSet<string> uncoveredCourses = new HashSet<string>();

        SimpleCSP Csp;

        /*************************   Init ***************************/

        void InitVariables()
        {
            Domain domDays = new Domain(0, _ObjectWithTimeTable.MAX_DAY-1);

            courseDVars.Clear();
            courseHVars.Clear();
            
            foreach (_Course c in tCourses)
            {
                int maxHour = 0;
                // find the maximal hour relevant for this course
                for (int d = 0; d < _ObjectWithTimeTable.MAX_DAY; ++d)
                {
                    for (int h = _ObjectWithTimeTable.MAX_HOUR - 1; h >= 0; --h)
                        if (c.is_on(d, h))
                        {
                            maxHour = Math.Max(maxHour, h);
                            break;
                        }
                }

                Domain domHours = new Domain(0, maxHour); // note that we are 0-based.

                for (int g = 0; g < c.Hours; ++g)
                {
                    Variable vd = new Variable(CourseVarName(true, c.Course_Type, c.Id, g), domDays);
                    Variable vh = new Variable(CourseVarName(false, c.Course_Type, c.Id, g), domHours);
                    courseDVars[vd.Name] = vd;
                    courseHVars[vh.Name] = vh;
                }
            }
            Log("# of slots to be scheduled: " + courseDVars.Count);
        }

        public void PreProcess()
        {
            tCourses = new List<TieSchedCourse>(courses.Count);
            courses.ForEach(delegate (_Course c)
            {
                tCourses.Add(new TieSchedCourse(c));
            });
            if (Program.flag_ChooseFreeDayForTeachers)  chooseFreeDatForTeachers();

            InitVariables();
            Csp = new SimpleCSP();
        }

        public void fromJSONString(string jsonString)
        {
            LoadFromJson(jsonString);
            PreProcess();
        }

        public void fromJSONFile(string fileName)
        {
            LoadFromFile(fileName);
            PreProcess();
        }

        public void chooseFreeDatForTeachers()
        {
            foreach (_Teacher t in teachers)
            {                
                for (int d = _ObjectWithTimeTable.MAX_DAY - 1; d >= 0; --d)
                {
                    bool freeDay = true;
                    for (int h = 0; h < _ObjectWithTimeTable.MAX_HOUR; ++h)
                        if (t.is_on(d, h))
                        {
                            freeDay = false;
                            break;
                        }
                    if (freeDay)
                    {
                        t.freeDay = d;               
                        break;
                    }
                }
            }
        }
        

        /*************************   Utils ***************************/

        private void Log(string v)
        {
            GlobalVar.Log.WriteLine(v);
        }

        public void printSolution()
        {
            Log("**********************************************************");
            Log("Solution:");
            foreach (_Course c in courses)
            {
                foreach (SolutionLine sl in c.Solution)
                    Log("Course:" + sl.group_id + ", day: " + sl.day + ", hour: " + sl.slot);
            }
            Log("**********************************************************");
        }


        Variable CourseVar(bool day, COURSE_TYPE_ENUM type, string courseID, int group)
        {
            Variable v = (Variable)(day ? courseDVars : courseHVars)[CourseVarName(day, type, courseID, group)];
            if (v == null)
                throw new Exception("Problem with CourseVar: " + courseID + ", " + type.ToString());
            return v;
        }

        string CourseVarName(bool day, COURSE_TYPE_ENUM type, string courseID, int group)
        {
            return (day ? "d_" : "h_") + CourseVarName(type, courseID, group);
        }

        string CourseVarName(COURSE_TYPE_ENUM type, string courseID, int group)
        {
            return (type == COURSE_TYPE_ENUM.F ? "" : type.ToString() + "_")  + courseID + "_" + group.ToString();
        }

   
      
        /// <summary>
        /// The representative of a cluster is the first that has the largest # of hours.
        /// We need this property because throught representative we constrain the groups from not overlapping. 
        /// Hence if we have two courses in the cluster with e.g., 3 and 4 groups, we need to constrain the 4 groups. 
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        _Course rep(_Course c)
        {
            if (c.Clusters.Count == 0) return c;
            int max = 0;
            int res = 0;
            for (int i = 0; i < c.Clusters[0].Courses.Count; ++i)
                if (c.Clusters[0].Courses[i].Hours > max)
                {
                    res = i;
                    max = c.Clusters[0].Courses[i].Hours;
                }
            return c.Clusters[0].Courses[res];
        }

        /*************************   Constraints ***************************/

        HashSet<Tuple<string, string, int, int>> nooverlap_constrained = new HashSet<Tuple<string, string, int, int>>();

        void con_noOverlap (_Course c1, _Course c2, int g1, int g2, string reason)
        {
            // We constrain the representatives. 
            _Course c1r = rep(c1), c2r = rep(c2);
            if (c1r.Id == c2r.Id && g1 == g2) return; //If c1, c2 are from the same cluster, no need to constrain. 

            // checking we did not have this constraint before. 
            Tuple<string, string, int, int> quad;
            if ((String.Compare(c1r.Id, c2.Id) < 0)) // convention: the pairs are alphabetically ordered, and their groups match the position. 
                quad = new Tuple<string, string, int, int>(c1r.Id, c2r.Id, g1, g2);
            else
                quad = new Tuple<string, string, int, int>(c2r.Id, c1r.Id, g2, g1);
            if (!nooverlap_constrained.Add(quad))
            {
                Log("removed redundant noOverlap");
                return;
            }

            
            Log("nooverlap(" + c1r.Name + " group " + g1 + "," + c2r.Name + " group " + g2 + "," + reason + ")");
            Variable vd1 = CourseVar(true, c1r.Course_Type, c1r.Id, g1);
            Variable vd2 = CourseVar(true, c2r.Course_Type, c2r.Id, g2);
            Variable vh1 = CourseVar(false, c1r.Course_Type, c1r.Id, g1);
            Variable vh2 = CourseVar(false, c2r.Course_Type, c2r.Id, g2);            

            CompositeConstraint c;
            c = new CompositeConstraint(BooleanOperator.OR, new Constraint[]{
                                            new VarVarConstraint(vd1, vd2, ArithmeticalOperator.NEQ),
                                            new VarVarConstraint(vh1, vh2, ArithmeticalOperator.NEQ) });
            if (Program.flag_SoftnoOverlap) c.Weight = 1;
            // Note we put in NegativeDisplayString the original courses c1,c2
            c.NegativeDisplayString = reason + ": no-overlap (" + c1.Name + " group " + g1 + "," + c2.Name+ " group " + g2 + ") ";
            Csp.Constraints.Add(c);
        }


      

        /// <summary>
        /// courses that have a shared class, shared teachers or sahred rooms cannot overlap
        /// </summary>
        void con_noOverlap()
        {
            // groups of the same course
            for (int i = 0; i < tCourses.Count; ++i)
            {        
                for (int g1 = 0; g1 < tCourses[i].Hours - 1; ++g1)
                    for (int g2 = g1 + 1; g2 < tCourses[i].Hours; ++g2)
                    {
                        con_noOverlap(tCourses[i], tCourses[i], g1, g2, "groups");
                    }
            }

            // groups of pairs of courses
            for (int i = 0; i < tCourses.Count - 1; ++i)
            {
                for (int j = i + 1; j < tCourses.Count; ++j)
                {
                    _Course c1 = tCourses[i], c2 = tCourses[j];                    

                    for (int g1 = 0; g1 < c1.Hours; ++g1)
                        for (int g2 = 0; g2 < c2.Hours; ++g2)
                        {
                            
                            /* Two F-type courses that have shared classes cannot overlap */
                            if (c1.Course_Type == COURSE_TYPE_ENUM.F && c2.Course_Type == COURSE_TYPE_ENUM.F &&
                                (c1.Classes.Intersect(c2.Classes)).Any())
                            {
                                string cl = c1.Classes.Intersect(c2.Classes).First().Name;
                                con_noOverlap(tCourses[i], tCourses[j], g1, g2, "classes (e.g., " + cl+")");
                                continue;
                            }
                            /* Two courses that have shared teachers cannot overlap */
                            if ((c1.Teachers.Intersect(c2.Teachers)).Any())
                            {
                                string t = c1.Teachers.Intersect(c2.Teachers).First().Name;
                                con_noOverlap(tCourses[i], tCourses[j], g1, g2, "teachers (e.g., " + t+ ")");
                                continue;
                            }
                            /* Two F-type courses that have shared rooms cannot overlap */
                            if (c1.Course_Type == COURSE_TYPE_ENUM.F && tCourses[j].Course_Type == COURSE_TYPE_ENUM.F &&
                                c1.Rooms != null && tCourses[j].Rooms != null &&
                                (c1.Rooms.Intersect(tCourses[j].Rooms)).Any())
                                con_noOverlap(tCourses[i], tCourses[j], g1, g2, "rooms");
                        }
                }
            }
        }

        HashSet<Tuple<string, int, int, int>> off_constrained = new HashSet<Tuple<string, int, int, int>>();

        void con_off(_Course c1, int group, int day, int hour, string reason)
        {
            _Course c1r = rep(c1);

            // checking we did not have this constraint before. 
            Tuple<string, int, int, int> quad = new Tuple<string, int, int, int>(c1r.Id,group,day,hour);
            if (!off_constrained.Add(quad))
            {
                Log("Removed redundant off constraints");
                return;
            }

            if (c1.Id != c1r.Id)
                Log("off(" + c1r.Name + "(representing " + c1.Name + ") group " + group + "," + day + "," + hour + "," + reason + ")");
            else
                Log("off(" + c1r.Name + " group " + group + "," + day + "," + hour + "," + reason + ")");

            Variable vd = CourseVar(true, c1r.Course_Type, c1r.Id, group);
            Variable vh = CourseVar(false, c1r.Course_Type, c1r.Id, group);
            CompositeConstraint c = new CompositeConstraint(BooleanOperator.OR,
                new Constraint[]
                {
                    new VarValConstraint(vd,day,ArithmeticalOperator.NEQ),
                    new VarValConstraint(vh,hour,ArithmeticalOperator.NEQ)
                });
            // note that in the negativeDisplayString we put the original course c1 and not c1r
            c.NegativeDisplayString = reason + ": (course = " + c1.Name + ", day = " + day + ", hour = " + hour + ")";
            Csp.Constraints.Add(c);
        }

        /// <summary>
        /// 'on' constraints 
        /// </summary>
        void con_off()
        {
            foreach (TieSchedCourse c in tCourses)
            {        
                for (int g = 0; g < c.Hours; ++g)
                    for (int d = 0; d < _ObjectWithTimeTable.MAX_DAY; ++d)
                        for (int h = 0; h < _ObjectWithTimeTable.MAX_HOUR; ++h)
                        {
                            if (!c.is_on(d, h))
                            {
                                con_off(c, g, d, h, "off");
                                continue;
                            }                       
                        }
            }
        }
             
        void con_ActiveOnDay(List<_Course> tHomeCourses, int d)
        {
            if (tHomeCourses.Count == 0) return;
            Log(tHomeCourses[0].Name + " = " + d);
            for (int i = 1; i < tHomeCourses.Count; ++i)
            {
                Log("\\/" + tHomeCourses[i].Name + " = " + d);
            }            
        }

        /// <summary>
        /// Teacher's active days in his own class. Currently forces each day to be active.
        /// </summary>
        void con_ActiveOnDay()
        {            
            foreach (_Teacher t in teachers)
            {
                List<_Course> tHomeCourses = new List<_Course>();
                // t's list of courses he teachers his own class
                foreach (_Course c in courses)
                {
                    if (!c.Classes.Contains(t.MyClass)) continue; // course not given to t's class. 
                    if (!c.Teachers.Contains(t)) continue; // t is not the teacher of that course
                    tHomeCourses.Add(c);
                }
                if (tHomeCourses.Count == 0) continue;
                for (int d = 1; d < _ObjectWithTimeTable.MAX_DAY; ++d)
                {
                    bool on_thatDay = false;
                    // checking t's availability on that day
                    for (int h = 1; h < _ObjectWithTimeTable.MAX_HOUR; ++h)
                    {
                        if (t.is_on(d,h))
                        {
                            on_thatDay = true;
                            break;
                        }
                    }
                    if (on_thatDay)
                        con_ActiveOnDay(tHomeCourses, d);
                }
            }
        }
        
        /*************************   TieLib interface ***************************/

        public SimpleCSP Translate()
        {
            Log("Translating.....");
            
            con_noOverlap();
            con_off();

            

            // Soft constraints:
            //con_ActiveOnDay();

            return Csp;
        }


        public void ExportSolution(Hashtable cspSolution)
        {
            /* Courses */
            foreach (_Course c in courses)
                for (int g = 0; g < c.Hours; ++g)
            {                    
                    _Course course = rep(c); // taking the schedule of the cluster's representative. 
                    var d = CourseVar(true, course.Course_Type, course.Id, g);
                    if (uncoveredCourses.Contains(d.Name.Substring(2)))
                    {
                        Log("Not reporting " + c.Name + " group " + g);
                        continue;
                    }
                    var h = CourseVar(false, course.Course_Type, course.Id, g);                    
                    c.AddSolutionLine((int)cspSolution[d], (int)cspSolution[h]);
            }            
        }
    }
}
