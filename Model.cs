using System;
using System.Collections.Generic;
using System.Linq;
using CourseScheduling;
using System.Collections;

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

        SimpleCSP Csp;

        /*************************   Init ***************************/

        void InitVariables()
        {
            Domain domDays = new Domain(0, _ObjectWithTimeTable.MAX_DAY-1);
            Domain domHours = new Domain(0, _ObjectWithTimeTable.MAX_HOUR-1);            

            courseDVars.Clear();
            courseHVars.Clear();
            foreach (_Course c in tCourses)
            {
                for (int i = 0; i < c.cHours; ++i)
                {
                    Variable vd = new Variable(CourseVarName(true, c.Course_Type, c.Id, i), domDays);
                    Variable vh = new Variable(CourseVarName(false, c.Course_Type, c.Id, i), domHours);
                    courseDVars[vd.Name] = vd;
                    courseHVars[vh.Name] = vh;
                }
            }
        }

        public void PreProcess()
        {
            tCourses = new List<TieSchedCourse>(courses.Count);
            courses.ForEach(delegate (_Course c)
            {
                tCourses.Add(new TieSchedCourse(c));
            });
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

        /*************************   Utils ***************************/

        private void Log(string v)
        {
            GlobalVar.Log.WriteLine(v);
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
        
        /*************************   Constraints ***************************/

        void con_noOverlap (int c1, int c2, int g1, int g2, string reason)
        {
            Log("nooverlap(" + tCourses[c1].Name + " group " + g1 + "," + tCourses[c2].Name + " group " + g2 + "," + reason + ")");
            Variable vd1 = CourseVar(true, tCourses[c1].Course_Type, tCourses[c1].Id, g1);
            Variable vd2 = CourseVar(true, tCourses[c2].Course_Type, tCourses[c2].Id, g2);
            Variable vh1 = CourseVar(false, tCourses[c1].Course_Type, tCourses[c1].Id, g1);
            Variable vh2 = CourseVar(false, tCourses[c2].Course_Type, tCourses[c2].Id, g2);            

            CompositeConstraint c;
            c = new CompositeConstraint(BooleanOperator.OR, new Constraint[]{
                                            new VarVarConstraint(vd1, vd2, ArithmeticalOperator.NEQ),
                                            new VarVarConstraint(vh1, vh2, ArithmeticalOperator.NEQ) });
            c.NegativeDisplayString = reason + ": no-overlap (" + tCourses[c1].Name + "," + tCourses[c2].Name+") ";
            Csp.Constraints.Add(c);
        }

        /// <summary>
        /// courses that have a shared class, shared teachers or sahred rooms cannot overlap
        /// </summary>
        void con_noOverlap()
        {
            for (int i = 0; i < tCourses.Count - 1; ++i)
            {
                for (int j = i + 1; j < tCourses.Count; ++j)
                {
                    _Course c1 = tCourses[i], c2 = tCourses[j];
                    for (int g1 = 0; g1 < c1.cHours; ++g1)
                        for (int g2 = 0; g2 < c2.cHours; ++g2)
                        {
                            
                            /* Two F-type courses that have shared classes cannot overlap */
                            if (c1.Course_Type == COURSE_TYPE_ENUM.F && c2.Course_Type == COURSE_TYPE_ENUM.F &&
                                (c1.Classes.Intersect(c2.Classes)).Any())
                            {
                                string cl = c1.Classes.Intersect(c2.Classes).First().Name;
                                con_noOverlap(i, j, g1, g2, "classes (e.g., " + cl+")");
                                continue;
                            }
                            /* Two courses that have shared teachers cannot overlap */
                            if ((c1.Teachers.Intersect(c2.Teachers)).Any())
                            {
                                string t = c1.Teachers.Intersect(c2.Teachers).First().Name;
                                con_noOverlap(i, j,g1, g2, "teachers (e.g., " + t+ ")");
                                continue;
                            }
                            /* Two F-type courses that have shared rooms cannot overlap */
                            if (c1.Course_Type == COURSE_TYPE_ENUM.F && tCourses[j].Course_Type == COURSE_TYPE_ENUM.F &&
                                c1.Rooms != null && tCourses[j].Rooms != null &&
                                (c1.Rooms.Intersect(tCourses[j].Rooms)).Any())
                                con_noOverlap(i, j, g1, g2, "rooms");
                        }
                }
            }
        }

        void con_off(_Course c1, int group, int day, int hour, string reason)
        {
            Log("off(" + c1.Name + " group " + group + "," + day + "," + hour + "," + reason + ")");

            Variable vd = CourseVar(true, c1.Course_Type, c1.Id, group);
            Variable vh = CourseVar(false, c1.Course_Type, c1.Id, group);
            CompositeConstraint c = new CompositeConstraint(BooleanOperator.OR,
                new Constraint[]
                {
                    new VarValConstraint(vd,day,ArithmeticalOperator.NEQ),
                    new VarValConstraint(vh,hour,ArithmeticalOperator.NEQ)
                });
            c.NegativeDisplayString = reason + ": (course = " + c1.Name + ", day = " + day + ", hour = " + hour + ")";
            Csp.Constraints.Add(c);
        }

        /// <summary>
        /// 'on' constraints 
        /// </summary>
        void con_off()
        {
            foreach (TieSchedCourse c in tCourses)
                for (int g = 0; g < c.cHours; ++g)
                for (int d = 0; d < _ObjectWithTimeTable.MAX_DAY; ++d)
                    for (int h = 0; h < _ObjectWithTimeTable.MAX_HOUR; ++h)
                        if (!c.is_on(d, h)) con_off(c, g, d, h, "off");
        }

        void con_Cluster(_Course c1, _Course c2, int g1, int g2, string reason)
        {
            Log("cluster-overlap(" + c1.Name + " group " + g1 + "," + c2.Name + " group " + g2 + "," + reason + ")");
            Variable vd1 = CourseVar(true, c1.Course_Type, c1.Id, g1);
            Variable vd2 = CourseVar(true, c2.Course_Type, c2.Id, g2);
            Variable vh1 = CourseVar(false, c1.Course_Type, c1.Id, g1);
            Variable vh2 = CourseVar(false, c2.Course_Type, c2.Id, g2);

            CompositeConstraint c;
            c = new CompositeConstraint(BooleanOperator.AND, new Constraint[]{
                                            new VarVarConstraint(vd1, vd2, ArithmeticalOperator.EQ),
                                            new VarVarConstraint(vh1, vh2, ArithmeticalOperator.EQ) });
            c.NegativeDisplayString = reason + ": cluster-overlap (" + c1.Name + "," + c2.Name + ") ";
            Csp.Constraints.Add(c);
        }

        void con_clusters()
        {
            foreach (_Cluster cluster in clusters) 
                for (int i = 0; i < cluster.Courses.Count - 1; ++i)
                    {
                        _Course c1 = cluster.Courses[i], c2 = cluster.Courses[i+1];
                        int min = Math.Min(c1.cHours, c2.cHours);
                        for (int k = 0; k<min;++k)
                        {
                            con_Cluster(c1, c2, k, k, "cluster " + cluster.Name);
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
            con_clusters();
            // Soft constraints:
            //con_ActiveOnDay();

            return Csp;
        }


        public void ExportSolution(Hashtable cspSolution)
        {
            /* Courses */
            foreach (_Course c in courses)
                for (int g = 0; g < c.cHours; ++g)
            {
                    var d = CourseVar(true, c.Course_Type, c.Id, g);
                    var h = CourseVar(false, c.Course_Type, c.Id, g);
                    c.AddSolutionLine((int)cspSolution[d], (int)cspSolution[h]);
            }

            /* Clusters */
            foreach (_Cluster cluster in clusters)
            {
                foreach (_Course c in cluster.Courses)
                {
                    //var d = CourseVar(true, c.Course_Type, c.Id);
                    //var h = CourseVar(false, c.Course_Type, c.Id);
                    //c.ttDay = (int)cspSolution[d];
                    //c.ttHour = (int)cspSolution[h];
                    //c.AddSolutionLine((int)cspSolution[d], (int)cspSolution[h]);
                }
            }
        }
    }
}
