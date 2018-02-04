using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchoolyConnect
{
    class TieSchedCourse : _Course
    {
        public string my_private_prop;

        public void My_private_method()
        {
            Console.WriteLine("Course " + Name + " with " +  Hours.ToString() + " hours " +   my_private_prop);
        }

        public TieSchedCourse(_Course c):base(c)
        {
            my_private_prop = "Created On " + DateTime.Now.ToLongTimeString();
        }
    }

    class TieSchedModel : ModelBase
    {
        List<TieSchedCourse> tCourses;
        

        public void PreProcess()
        {
            tCourses = new List<TieSchedCourse>(courses.Count);
            courses.ForEach(delegate (_Course c)
            {
                tCourses.Add(new TieSchedCourse(c));
            });

        }

        void con_noOverlap (string c1, string c2, string reason)
        {
            Console.WriteLine("nooverlap(" + c1 + "," + c2 + "," + reason + ")");
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
                        con_noOverlap(tCourses[i].Name, tCourses[j].Name, "classes");
                    /* Two courses that have shared teachers cannot overlap */
                    if ((tCourses[i].Teachers.Intersect(tCourses[j].Teachers)).Any())
                        con_noOverlap(tCourses[i].Name, tCourses[j].Name, "teachers");
                    /* Two F-type courses that have shared rooms cannot overlap */
                    if (tCourses[i].Course_Type == COURSE_TYPE_ENUM.F && tCourses[j].Course_Type == COURSE_TYPE_ENUM.F &&
                        tCourses[i].Rooms != null && tCourses[j].Rooms != null &&
                        (tCourses[i].Rooms.Intersect(tCourses[j].Rooms)).Any())
                        con_noOverlap(tCourses[i].Name, tCourses[j].Name, "rooms");
                }
            }
        }

        void con_off(string c1, int day, int hour, string reason)
        {
            Console.WriteLine("off(" + c1 + "," + day + "," + hour + "," + reason + ")");
        }

        /// <summary>
        /// 'on' constraints 
        /// </summary>
        void con_off()
        {
            foreach (TieSchedCourse c in tCourses)
                for (int i = 1; i < _ObjectWithTimeTable.MAX_DAY; ++i)
                    for (int j = 1; j < _ObjectWithTimeTable.MAX_SLOT; ++j)
                        if (!c.is_on(i, j)) con_off(c.Name, i, j, "off");
        }

        void con_ActiveOnDay(List<_Course> tHomeCourses, int d)
        {
            if (tHomeCourses.Count == 0) return;
            Console.Write(tHomeCourses[0].Name + " = " + d);
            for (int i = 1; i < tHomeCourses.Count; ++i)
            {
                Console.Write("\\/" + tHomeCourses[i] + " = " + d);
            }
        }

        /// <summary>
        /// Teacher's active days in his own class
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
                for (int d = 1; d < _ObjectWithTimeTable.MAX_DAY; ++d)
                {
                    con_ActiveOnDay(tHomeCourses, d);
                }
            }
        }

        public void Solve()
        {
            Console.WriteLine("Solving.....");
            tCourses.ForEach(delegate (TieSchedCourse tc)
            {
                tc.My_private_method();
            });


            con_noOverlap();

            con_off();

            con_ActiveOnDay();
            

            SaveSolution();
        }

     

        public void fromJSON(string fileName)
        {
            Load(fileName);
            PreProcess();
            
        }

    }
}
