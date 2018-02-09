﻿using System;
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
            chooseFreeDatForTeachers();

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

        /// <summary>
        /// Mark all but the first course in each cluster as type 'R'. We will later ignore these courses in the constraints. 
        /// </summary>
        void filterClusterCourses()
        {
            foreach (_Cluster cluster in clusters)
                for (int i = 1; i < cluster.Courses.Count; ++i)
                {
                    TieSchedCourse c = tCourses.Find(course => course.Id == cluster.Courses[i].Id);
                    cluster.Courses[i].Course_Type = c.Course_Type = COURSE_TYPE_ENUM.R;
                    Debug.Assert(cluster.Courses[i].Clusters.Count == 1); // does not makes sense that a course is in more than one cluster (because then we can unite the clusters)
                }
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
            c.Weight = 1;
            c.NegativeDisplayString = reason + ": no-overlap (" + tCourses[c1].Name + "," + tCourses[c2].Name+") ";
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
                if (tCourses[i].Course_Type == COURSE_TYPE_ENUM.R) continue;
                for (int g1 = 0; g1 < tCourses[i].Hours - 1; ++g1)
                    for (int g2 = g1 + 1; g2 < tCourses[i].Hours; ++g2)
                    {
                        con_noOverlap(i, i, g1, g2, "groups");
                    }
            }

            // groups of pairs of courses
            for (int i = 0; i < tCourses.Count - 1; ++i)
            {
                for (int j = i + 1; j < tCourses.Count; ++j)
                {
                    _Course c1 = tCourses[i], c2 = tCourses[j];
                    if (c1.Course_Type == COURSE_TYPE_ENUM.R || c2.Course_Type == COURSE_TYPE_ENUM.R) continue;                   

                    for (int g1 = 0; g1 < c1.Hours; ++g1)
                        for (int g2 = 0; g2 < c2.Hours; ++g2)
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
            {
                if (c.Course_Type == COURSE_TYPE_ENUM.R) continue;
                for (int g = 0; g < c.Hours; ++g)
                    for (int d = 0; d < _ObjectWithTimeTable.MAX_DAY; ++d)
                        for (int h = 0; h < _ObjectWithTimeTable.MAX_HOUR; ++h)
                        {
                            if (!c.is_on(d, h))
                            {
                                con_off(c, g, d, h, "off");
                                continue;
                            }
                            // it is possible that this course represents other courses, that are not on
                            if (c.Clusters.Count > 0)
                            {
                                foreach (_Course crs in c.Clusters[0].Courses)
                                    if (!crs.is_on(d, h))
                                    {
                                        con_off(c, g, d, h, "off");
                                        break;
                                    }
                            }
                        }
            }
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
                        int min = Math.Min(c1.Hours, c2.Hours);
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

            // Either this:
            // con_clusters();
            // or this: 
            filterClusterCourses();

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
                    _Course course = c;
                    // completing missing assignment for type-R courses.
                    if (c.Course_Type == COURSE_TYPE_ENUM.R) course = c.Clusters[0].Courses[0]; // taking the schedule of the cluster's representative. 
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
