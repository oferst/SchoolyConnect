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


        void InitVariables()
        {
            Domain domDays = new Domain(0, _ObjectWithTimeTable.MAX_DAY-1);
            Domain domHours = new Domain(0, _ObjectWithTimeTable.MAX_HOUR-1);            

            courseDVars.Clear();
            courseHVars.Clear();
            foreach (_Course c in tCourses)
            {
                Variable vd = new Variable(CourseVarName(true, c.Course_Type, c.Id), domDays);
                Variable vh = new Variable(CourseVarName(false, c.Course_Type, c.Id), domHours);
                courseDVars[vd.Name] = vd;
                courseHVars[vh.Name] = vh;
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

        Variable CourseVar(bool day, COURSE_TYPE_ENUM type, string courseID)
        {
            Variable v = (Variable)(day ? courseDVars : courseHVars)[CourseVarName(day, type, courseID)];
            if (v == null)
                throw new Exception("Course metadata doesn't match course data: " + courseID + ", " + type.ToString());
            return v;
        }

        string CourseVarName(bool day, COURSE_TYPE_ENUM type, string courseID)
        {
            return (day ? "d_" : "h_") + CourseVarName(type, courseID);
        }

        string CourseVarName(COURSE_TYPE_ENUM type, string courseID)
        {
            return (type == COURSE_TYPE_ENUM.F ? "" : type.ToString() + "_")  + courseID;
        }


        void con_noOverlap (int c1, int c2, string reason)
        {
            Log("nooverlap(" + tCourses[c1].Name + "," + tCourses[c2].Name + "," + reason + ")");
            Variable vd1 = CourseVar(true, tCourses[c1].Course_Type, tCourses[c1].Id);
            Variable vd2 = CourseVar(true, tCourses[c2].Course_Type, tCourses[c2].Id);
            Variable vh1 = CourseVar(false, tCourses[c1].Course_Type, tCourses[c1].Id);
            Variable vh2 = CourseVar(false, tCourses[c2].Course_Type, tCourses[c2].Id);            

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
                    /* Two F-type courses that have shared classes cannot overlap */
                    if (tCourses[i].Course_Type == COURSE_TYPE_ENUM.F && tCourses[j].Course_Type == COURSE_TYPE_ENUM.F &&
                        (tCourses[i].Classes.Intersect(tCourses[j].Classes)).Any())
                    {
                        con_noOverlap(i, j, "classes");
                        continue;
                    }
                    /* Two courses that have shared teachers cannot overlap */
                    if ((tCourses[i].Teachers.Intersect(tCourses[j].Teachers)).Any())
                    {
                        con_noOverlap(i, j, "teachers");
                        continue;
                    }
                    /* Two F-type courses that have shared rooms cannot overlap */
                    if (tCourses[i].Course_Type == COURSE_TYPE_ENUM.F && tCourses[j].Course_Type == COURSE_TYPE_ENUM.F &&
                        tCourses[i].Rooms != null && tCourses[j].Rooms != null &&
                        (tCourses[i].Rooms.Intersect(tCourses[j].Rooms)).Any())
                        con_noOverlap(i,j, "rooms");
                }
            }
        }

        void con_off(_Course c1, int day, int hour, string reason)
        {
            Log("off(" + c1 + "," + day + "," + hour + "," + reason + ")");

            Variable vd = CourseVar(true, c1.Course_Type, c1.Id);
            Variable vh = CourseVar(false, c1.Course_Type, c1.Id);
            CompositeConstraint c = new CompositeConstraint(BooleanOperator.OR,
                new Constraint[]
                {
                    new VarValConstraint(vd,day,ArithmeticalOperator.NEQ),
                    new VarValConstraint(vh,hour,ArithmeticalOperator.NEQ)
                });
            c.NegativeDisplayString = reason + ": (course = " + c1.Name + ", day = " + day + ", hour = " + hour + ")";
            Csp.Constraints.Add(c);
        }

        private void Log(string v)
        {
            GlobalVar.Log.WriteLine(v);
        }

        /// <summary>
        /// 'on' constraints 
        /// </summary>
        void con_off()
        {
            foreach (TieSchedCourse c in tCourses)
                for (int d = 0; d < _ObjectWithTimeTable.MAX_DAY; ++d)
                    for (int h = 0; h < _ObjectWithTimeTable.MAX_HOUR; ++h)
                        if (!c.is_on(d, h)) con_off(c, d, h, "off");
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

        public SimpleCSP Translate()
        {
            Log("Translating.....");

            con_noOverlap();
            con_off();

            // Soft constraints:
            con_ActiveOnDay();

            return Csp;
        }

     

        public void fromJSON(string fileName)
        {
            Load(fileName);
            PreProcess();
            
        }

    }
}
