using System;
using System.Collections.Generic;
using System.Linq;
using CourseScheduling;
using System.Collections;
using System.Diagnostics;
using System.IO;
using static System.Net.Mime.MediaTypeNames;

namespace SchoolyConnect
{
    class TieSchedCourse : _Course
    {
        public string my_private_prop;
           
        public TieSchedCourse(_Course c) : base(c)
        {
            my_private_prop = "Created On " + DateTime.Now.ToLongTimeString();
        }
    }

    public class TieSchedModel : ModelBase
    {
        List<TieSchedCourse> tCourses;
        Hashtable courseDVars = new Hashtable();
        Hashtable courseHVars = new Hashtable();

        public Hashtable getCourseDVars { get { return courseDVars; } }
        public Hashtable getCourseHVars { get { return courseHVars; } }

        // Here we will fill courses that violate nooverlap constraints (should be hard constrained, but we make them soft).
        public HashSet<string> uncoveredCourses = new HashSet<string>();

        public SimpleCSP Csp;

        /*************************   Init ***************************/

        void InitVariables()
        {
            int counterFrontal = 0;
            Domain domDays = new Domain(0, _ObjectWithTimeTable.MAX_DAY - 1);

            courseDVars.Clear();
            courseHVars.Clear();
            bool soft = false;

            foreach (_Course c in tCourses)
            {
                int maxHour = 0;
                // find the maximal hour relevant for this course
                for (int d = 0; d < _ObjectWithTimeTable.MAX_DAY; ++d)
                {
                    for (int h = _ObjectWithTimeTable.MAX_HOUR - 1; h >= 0; --h)
                        if (c.is_on(d, h, ref soft))
                        {
                            maxHour = Math.Max(maxHour, h);
                            break;
                        }
                }

                Domain domHours = new Domain(0, maxHour); // note that we are 0-based.
                Log(c.Name + " " + maxHour);
                for (int g = 0; g < c.Hours; ++g)
                {
                    Variable vd = new Variable(CourseVarName(true, c.Course_Type, c.Id, g), domDays);
                    if (c.Course_Type == COURSE_TYPE_ENUM.F) counterFrontal++;
                    Variable vh = new Variable(CourseVarName(false, c.Course_Type, c.Id, g), domHours);
                    courseDVars[vd.Name] = vd;
                    courseHVars[vh.Name] = vh;
                }
            }
            Log("# of slots to be scheduled: " + courseDVars.Count + " from them " + counterFrontal + "are type F");

        }

        public void PreProcess()
        {
            tCourses = new List<TieSchedCourse>(courses.Count);
            courses.ForEach(delegate (_Course c)
            {
                tCourses.Add(new TieSchedCourse(c));
            });
            if (Program.flag_ChooseFreeDayForTeachers) chooseFreeDatForTeachers();

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
                    Log("Course:" + getCourse(sl.group_id).Name + ", day: " + sl.day + ", hour: " + sl.slot);
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

        public string CourseVarName(bool day, COURSE_TYPE_ENUM type, string courseID, int group)
        {
            return (day ? "d_" : "h_") + CourseVarName(type, courseID, group);
        }

        string CourseVarName(COURSE_TYPE_ENUM type, string courseID, int group)
        {
            return (type == COURSE_TYPE_ENUM.F ? "" : type.ToString() + "_") + courseID + "_" + group.ToString();
        }

        string ClassXVarName(string ID, int day, int hour)
        {
            return "x_" + ID + "_" + day.ToString() + "_" + hour.ToString();

        }
        public Variable ClassXVar(string ID, int day, int hour)
        {
            Variable v = (Variable)(courseDVars)[ClassXVarName(ID, day, hour)];
            if (v == null)
                throw new Exception("Problem with XVar: " + ID + "," + day + "," + hour);
            return v;
        }


        /// <summary>
        /// The representative of a cluster is the first that has the largest # of hours.
        /// We need this property because throught representative we constrain the groups from not overlapping. 
        /// Hence if we have two courses in the cluster with e.g., 3 and 4 groups, we need to constrain the 4 groups. 
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public _Course rep(_Course c)
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


        void con_noOverlap(_Course c1, _Course c2, int g1, int g2, string reason)
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
            if (Program.flag_SoftnoOverlap) c.Weight = 3;

            // Note we put in NegativeDisplayString the original courses c1,c2
            // also note that we search for the term 'no-overlap' in 'CheckSolution' so do not change it. 
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
                                con_noOverlap(tCourses[i], tCourses[j], g1, g2, "classes (e.g., " + cl + ")");
                                continue;
                            }
                            /* Two courses that have shared teachers cannot overlap */
                            if ((c1.Teachers.Intersect(c2.Teachers)).Any())
                            {
                                string t = c1.Teachers.Intersect(c2.Teachers).First().Name;
                                con_noOverlap(tCourses[i], tCourses[j], g1, g2, "teachers (e.g., " + t + ")");
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

        void con_off(_Course c1, int group, int day, int hour, string reason, bool soft = false)
        {
            _Course c1r = rep(c1);

            // checking we did not have this constraint before. 
            Tuple<string, int, int, int> quad = new Tuple<string, int, int, int>(c1r.Id, group, day, hour);
            if (!off_constrained.Add(quad))
            {
                Log("Removed redundant off constraints");
                return;
            }

            Variable vd = CourseVar(true, c1r.Course_Type, c1r.Id, group);
            Variable vh = CourseVar(false, c1r.Course_Type, c1r.Id, group);
            CompositeConstraint c = new CompositeConstraint(BooleanOperator.OR,
                new Constraint[]
                {
                    new VarValConstraint(vd,day,ArithmeticalOperator.NEQ),
                    new VarValConstraint(vh,hour,ArithmeticalOperator.NEQ)
                });
            if (soft && hour == 6) c.Weight = Program.weight_hour6; 
            if (soft && hour == 7) c.Weight = Program.weight_hour7;
            if (soft && hour == 8) c.Weight = Program.weight_hour8;
            
            if (soft && c1.Classes.Count > 0 && c1.Teachers.Count > 0 && c1.Teachers.Contains(c1.Classes[0].myTeacher))
                c.Weight += Program.weight_homeTeacherOnLateHour; // we want home teachers to be early so we can later move others to the homeTecher's free day. 
            // note that in the negativeDisplayString we put the original course c1 and not c1r
            if (c1.Id != c1r.Id)
                Log("off(" + c1r.Name + "(representing " + c1.Name + ") group " + group + "," + day + "," + hour + "," + reason + "weight " + c.Weight + ")");
            else
                Log("off(" + c1r.Name + " group " + group + "," + day + "," + hour + "," + reason + "weight " +  c.Weight + ")");
            c.NegativeDisplayString = reason + ": (course = " + c1.Name + ", day = " + day + ", hour = " + hour + " weight = " + c.Weight + ")";
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
                            bool soft = false;
                            if (!c.is_on(d, h, ref soft))
                            {
                                con_off(c, g, d, h, "off");                                
                                continue;
                            }
                            if (soft)
                                con_off(c, g, d, h, "off", true);
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
                if (t.MyClass == null) continue;
                // t's list of courses he teaches his own class
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
                        if (t.is_on(d, h))
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


        /// <summary>
        /// Not more than c.Max_Daily_Hours per day.
        /// </summary>
        /// <param name="c"></param>
        void con_maxHours(_Course c) // suppose c.Hours = 6, c.Max_Daily_Hours = 2;
        {
            int partitionSize = (int)Math.Ceiling(((float)c.Hours / c.Max_Daily_Hours)); // = 3
            int g = 0;
            while (g < c.Hours)
            {
                for (int p1 = 0; p1 < partitionSize - 1 && g + p1 < c.Hours; ++p1)
                    for (int p2 = p1 + 1; p2 < partitionSize && g + p2 < c.Hours; ++p2)
                    { // all pairs (0,1) (0,2) (1,2)
                        Variable vd1 = CourseVar(true, c.Course_Type, c.Id, g + p1);
                        Variable vd2 = CourseVar(true, c.Course_Type, c.Id, g + p2);
                        Csp.Constraints.Add(new VarVarConstraint(vd1, vd2, ArithmeticalOperator.NEQ) { NegativeDisplayString = "Max_Daily_Hours: " + c.Name });
                        Log("Max_Daily_Hours (" + c.Hours + "," + c.Max_Daily_Hours + ")" + c.Id + " " + (g + p1).ToString() + " " + (g + p2).ToString());
                    }
                g += partitionSize;
            }
        }

        /// <summary>
        /// Exactly \floor(c.Max_Daily_Hours per day) per day c is scheduled.
        /// </summary>
        /// <param name="c"></param>
        void con_maxHoursExact(_Course c)
        {
            List<Variable> diffDay = new List<Variable>(); // collects the first of each partition
            for (int g = 0; g < c.Hours; ++g)
            {
                Variable vd_current = CourseVar(true, c.Course_Type, c.Id, g);
                diffDay.Add(vd_current);
                // same day inside the partition:
                for (int i = 0; i < c.Max_Daily_Hours - 1 && g < c.Hours - 1; ++i)
                {
                    Variable vd_next = CourseVar(true, c.Course_Type, c.Id, g + 1);
                    Csp.Constraints.Add(new VarVarConstraint(vd_current, vd_next, ArithmeticalOperator.EQ) { NegativeDisplayString = "partition (same day)" });
                    vd_current = vd_next;
                    ++g;
                }
            }

            // partitions are on different days 
            for (int v1 = 0; v1 < diffDay.Count - 1; ++v1)
                for (int v2 = v1 + 1; v2 < diffDay.Count; ++v2)
                {
                    Csp.Constraints.Add(new VarVarConstraint(diffDay[v1], diffDay[v2], ArithmeticalOperator.NEQ) { NegativeDisplayString = "partition (diff day)" });
                }
        }

        void con_maxHours()
        {
            foreach (_Course c in courses)
            {
                if (c.Course_Type != COURSE_TYPE_ENUM.F) continue;
                if (c.Hours <= 1) continue;
                if (c.Max_Daily_Hours >= c.Hours) continue;
                Debug.Assert(c.Max_Daily_Hours > 0);
                if (!Program.flag_constrainAllMaxHours && c.Max_Daily_Hours != 1) continue; 

                List<_Teacher> classHomeTeachers = new List<_Teacher>();
                foreach (_Class cl in c.Classes) classHomeTeachers.Add(cl.myTeacher);
                if (c.Teachers.Intersect(classHomeTeachers).Any()) continue; // if a homeTeacher of one of c's classes teaches this course, then we do not apply restrictions.
                if (c.Id != rep(c).Id) continue; // assuming in a cluster the representative has the same max_hour constraint anyway.

                // con_maxHoursExact(c);
                con_maxHours(c);
            }
        }

/// <summary>
/// Soft constraint: non-home-teacher courses should be given on the home-teacher free day.  
/// </summary>
        void con_HomeTeacherAbsentDay()
        {
            foreach (_Teacher t in teachers)
            {
                List<_Course> tNonHomeCourses = new List<_Course>(); // The courses given to class A, not by A's home-Teacher. 
                if (t.MyClass == null) continue;
                // t's list of courses he teaches his own class
                foreach (_Course c in courses)
                {
                    if (!c.Classes.Contains(t.MyClass)) continue; // course not given to t's class. 
                    if (c.Teachers.Contains(t)) continue; // t is not the teacher of that course
                    tNonHomeCourses.Add(c);
                }
                // we now search for T's free day
                for (int d = 1; d < _ObjectWithTimeTable.MAX_DAY; ++d)
                {
                    bool on_thatDay = false;
                    // checking t's availability on that day
                    for (int h = 1; h < _ObjectWithTimeTable.MAX_HOUR; ++h)
                    {
                        if (t.is_on(d, h))
                        {
                            on_thatDay = true;
                            break;
                        }
                    }
                    if (!on_thatDay)
                    {
                        foreach (_Course c in tNonHomeCourses)
                        {
                            for (int g = 0; g < c.Hours; ++g)
                            {
                                Variable vd = CourseVar(true, c.Course_Type, c.Id, g);
                                VarValConstraint con = new VarValConstraint(vd, d, ArithmeticalOperator.EQ, Program.weight_nonHomeTeacherCoursesonFreeDay);
                                con.NegativeDisplayString = "HomeTeacherAbsentDay: (" + c.Name + ", group " + g + " day = " + d + ")";
                                Log(con.NegativeDisplayString);
                                Csp.Constraints.Add(con);
                            }

                        }
                    }
                }
            }
        }

       /// <summary>
       /// Explicit no-gap constraints. 
       /// </summary>
        void con_gaps()
        {
            Domain domx = new Domain(0, 1);
            
            foreach (_Class cl in classes)
            {
                for (int d = 0; d < _ObjectWithTimeTable.MAX_DAY; ++d)
                {
                    Variable prev_vx = null;
                    for (int h = 1; h < _ObjectWithTimeTable.MAX_HOUR; ++h)
                    {
                        CompositeConstraint disj = new CompositeConstraint(BooleanOperator.OR);
                        foreach (_Course c in courses)
                        {
                            if (c != rep(c)) continue;
                            for (int g = 0; g < c.Hours; ++g)
                            {
                                if (!c.Classes.Contains(cl)) continue;
                                Variable vd = CourseVar(true, c.Course_Type, c.Id, g);
                                Variable vh = CourseVar(false, c.Course_Type, c.Id, g);
                                CompositeConstraint conj = new CompositeConstraint(BooleanOperator.AND);
                                conj.SubConstraints.Add(new VarValConstraint(vd, d, ArithmeticalOperator.EQ));
                                conj.SubConstraints.Add(new VarValConstraint(vh, h, ArithmeticalOperator.EQ));
                                disj.SubConstraints.Add(conj);
                            }
                        }
                        if (disj.SubConstraints.Count == 0) continue;
                        Variable vx = new  Variable(ClassXVarName(cl.Id, d, h), domx);
                        courseDVars[vx.Name] = vx;
                        // x-> disj
                        CompositeConstraint dir1 = new CompositeConstraint(BooleanOperator.OR);
                        dir1.SubConstraints.Add(new VarValConstraint(vx, 0, ArithmeticalOperator.EQ)); // !x 
                        dir1.SubConstraints.Add(disj);
                        // disj -> x
                        CompositeConstraint dir2 = new CompositeConstraint(BooleanOperator.OR);
                        dir2.SubConstraints.Add(new VarValConstraint(vx, 1, ArithmeticalOperator.EQ)); // x 
                        dir2.SubConstraints.Add(new CompositeConstraint(BooleanOperator.NOT, disj));
                        dir1.NegativeDisplayString = dir2.NegativeDisplayString = "gap:\n\t" + cl.Name + ",\n\t" + d + "," + h;

                        Log(dir1.NegativeDisplayString);
                        Csp.Constraints.Add(dir1);
                        Csp.Constraints.Add(dir2);
                        if (h == 1) prev_vx = vx;
                        else
                        {
                            // vx => prev_vx
                            CompositeConstraint implication = new CompositeConstraint(BooleanOperator.OR);
                            implication.SubConstraints.Add(new VarValConstraint(vx, 0, ArithmeticalOperator.EQ)); // !vx
                            implication.SubConstraints.Add(new VarValConstraint(prev_vx, 1, ArithmeticalOperator.EQ));
                            implication.NegativeDisplayString = "prevent gap: \n\t" + cl.Name + "\n\t," + d + "," + h + " => " + d + "," + (h - 1);
                            Log(implication.NegativeDisplayString);
                            Csp.Constraints.Add(implication);
                            prev_vx = vx;
                        }
                    }                    
                }
            }
        }


        /*************************   TieLib interface ***************************/

        public SimpleCSP Translate()
        {            
            //buildGraph();
            Log("Translating.....");
            con_maxHours();
            con_noOverlap();
            con_off();
            con_HomeTeacherAbsentDay();

            if (Program.flag_gaps_constraints) con_gaps();

            //con_ActiveOnDay();

            return Csp;
        }


        public void ExportSolution(Hashtable cspSolution)
        {
            /* Courses */
            int counter = 0;
            foreach (_Course c in courses)
                for (int g = 0; g < c.Hours; ++g)
                {
                    _Course course = rep(c); // taking the schedule of the cluster's representative. 
                    var d = CourseVar(true, course.Course_Type, course.Id, g);
                    
                    if (uncoveredCourses.Contains(d.Name.Substring(2)))
                    {
                        Log("Not reporting " + c.Name + " group " + g + ((course.Id != c.Id) ? "(cluster)" : ""));
                        continue;
                    }
                    var h = CourseVar(false, course.Course_Type, course.Id, g);
                    c.AddSolutionLine((int)cspSolution[d], (int)cspSolution[h]);
                    counter++;
                    //Log("Reporting " + c.Name + " group " + g + ((course.Id != c.Id) ? "(cluster)" : "" + " day: " + (int)cspSolution[d] + " h:" + (int)cspSolution[h]));
                }            

            Log("exported " + counter + " lines");
        }

        /************************  Graphs ************************/

        void buildGraph()
        {
            
            Dictionary<Tuple<string, string>, int> common_teachers = new Dictionary<Tuple<string, string>, int>();
            Dictionary<Tuple<string, string>, int> common_classes = new Dictionary<Tuple<string, string>, int>();
            Dictionary<Tuple<string, string>, int> common_rooms = new Dictionary<Tuple<string, string>, int>();

            // common teachers of classes (F classes). 
            for (int i = 0; i < classes.Count; ++i)
                for (int j = i + 1; j < classes.Count; ++j)
                {
                    char c1 = classes[i].Name[0], c2 = classes[j].Name[0];
                    if (c1 == c2) continue;
                    c1 = (char)((int)c1 + 'A' - 'א');
                    c2 = (char)((int)c2 + 'A' - 'א');
                    string layer1 = c1.ToString(), layer2 = c2.ToString();

                    List<_Course> 
                        cl1Courses = classCourses(classes[i]), 
                        cl2Courses = classCourses(classes[j]);
                    // The list of teachers of these classes: 
                    HashSet<_Teacher> cl1Teachers = new HashSet<_Teacher>(), cl2Teachers = new HashSet<_Teacher>();
                    foreach (_Course c in cl1Courses)
                        foreach (_Teacher t in c.Teachers) cl1Teachers.Add(t); 
                    foreach (_Course c in cl2Courses)
                        foreach (_Teacher t in c.Teachers) cl2Teachers.Add(t);
                    int num = cl1Teachers.Intersect(cl2Teachers).Count();
                    if (num > 0)
                    {   
                        Tuple<string, string> edge = new Tuple<string, string>(layer1, layer2);
                        if (common_teachers.Keys.Contains(edge)) 
                            common_teachers[edge] += num;
                        else common_teachers[edge] = num;
                    }
                }

            using (StreamWriter file = new StreamWriter(@"C:\temp\school.dot"))
            {
                
                file.WriteLine("graph school {");
                foreach (Tuple<string, string> edge in common_teachers.Keys)
                    file.WriteLine(edge.Item1 + " -- " + edge.Item2 + " [label = \"" + common_teachers[edge] + "\"]");
                file.WriteLine("}");
            }
        }

    }

}

