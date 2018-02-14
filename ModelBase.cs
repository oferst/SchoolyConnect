using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Windows.Forms;
using CourseScheduling;
using System.Diagnostics;

namespace SchoolyConnect
{
    public enum COURSE_TYPE_ENUM { F=1,S=2,P=3};


    public class SolutionLine
    {
        public string group_id { get; set; }
        public int day { get; set; }
        public int slot { get; set; }

        public SolutionLine(string aId, int aDay, int aSlot)
        {
            group_id = aId;
            day = aDay;
            slot = aSlot;
        }
    }


    public class _Object
    {
        
        private string id = "";
        private string name = "";

        public string Id { get => id; set => id = value; }
        public string Name { get => name; set => name = value; }
        
        protected _Object()
        {
            
        }
    }
    public class _ObjectWithTimeTable : _Object
    {
        public const int MAX_DAY = 6;
        public const int MAX_HOUR = 10;
        public int ttTotal => tt.Cast<bool>().Sum(b => { return b ? 1 : 0; });
        protected _ObjectWithTimeTable () : base()
        {
         
        }
            
        public bool[,] tt = new bool[MAX_DAY, MAX_HOUR]; // ofer changed to public

        public bool is_on (int day,int slot)
        {
            return tt[day, slot];
        }


    }
    public class _Subject: _Object
    {
        public _Subject() : base()
        {

        }

    }
    public class _Room : _ObjectWithTimeTable
    {
        public _Room() : base()
        {

        }

    }
    public class _Teacher : _ObjectWithTimeTable
    {
        private _Class myClass;
        public _Class MyClass { get => myClass; set => myClass = value; }
        public _Teacher() : base() // ofer changed to public
        {
            freeDay = -1;
        }
        public int freeDay; // ofer        
    }

    public class _Class : _ObjectWithTimeTable
    {
        public _Teacher myTeacher;

        public _Class (_Teacher t) : base() // ofer changed to public
        {
            myTeacher = t;            
        }
    }

    public class _Course : _Object
    {
        public int  Hours { get; set; }
        public int  cHours { get
            {
                return Hours * Classes.Count();
            }
        }
        public int tHours
        {
            get
            {
                return Hours * Teachers.Count();
            }
        }

        public COURSE_TYPE_ENUM  Course_Type { get; set; }
        public _Subject Subject { get; set; }
        public int  Max_Daily_Hours { get; set; }
        public List<_Teacher> Teachers { get; set; }
        public List<_Class> Classes { get; set; }
        public List<_Room> Rooms { get; set; }
        public List<_Cluster> Clusters { get; set; }
        public List<SolutionLine> Solution { get; set; }
        
        /******* From Solver ************/
        //public int ttDay { get; set; }
        //public int ttHour {get; set; }
        /*******************************/
        
        public _Course() : base()
        {
            Subject = null;
            Teachers = new List<_Teacher>();
            Classes = new List<_Class>();
            Rooms = new List<_Room>();
            Clusters = new List<_Cluster>();
            Solution = new List<SolutionLine>();

        
        }

        public _Course(_Course c) : base()
        {
            var props = typeof(_Course).GetProperties().Where(x => x.CanWrite).ToList();
            foreach (var p in props)
            {
                p.SetValue(this, p.GetValue(c, null), null);
            }
        }
        
        public void AddSolutionLine(int Day, int Slot)
        {
            Solution.Add(new SolutionLine(Id,Day,Slot));
        }

        public bool is_on(int day, int slot, ref bool soft) 
        {
            soft = false;
            if (Program.flag_ChooseFreeDayForTeachers)
            {
                foreach (var t in Teachers) if (t.freeDay == day) return false;
            }
            else
            {
                foreach (var t in Teachers) if (!t.is_on(day, slot)) return false;
            }
            if (Rooms != null) foreach (var t in Rooms) if (!t.is_on(day, slot)) return false;

            if (Classes != null)
            {
                if (slot > 8 && Course_Type != COURSE_TYPE_ENUM.S) return false;
                if (slot > 8 && Course_Type == COURSE_TYPE_ENUM.S) return false;
                // last hours are soft-constrained
                if ((slot == 8 || slot == 7) && Course_Type != COURSE_TYPE_ENUM.S) soft = true;
                if (slot == 8 && Course_Type == COURSE_TYPE_ENUM.S) soft = true;
                //if (!t.is_on(day, slot))  // original

                // !! experiment 1, freeing one hour if it is <= 6
                //foreach (var t in Classes)                
                //    if ((!t.is_on(day, slot) && slot >= 7) ||
                //        (!t.is_on(day, slot) && !t.is_on(day, slot - 1))
                //        )
                //        return false;
                //    else 
                //    if (!t.is_on(day, slot)) // the case that we permitted because of the experiment.
                //    {
                //        soft = true;                        
                //    }
            }
            

            return true;            
        }
    }



    public class _Cluster : _Object
    {
        /******* From Solver ************/
        public int ttDay { get; set; }
        public int ttHour { get; set; }
        /*********************************/
        

        public List<_Course> Courses { get; set; }

        public int Hours
        {
            get
            {
                return Courses.Max(x => x.Hours);
            }
        }

        public int tHours
        {
            get
            {
                return Courses.Sum(course => { return course.tHours; });
            }
        }



        public List<_Teacher> Teachers
        {
            get
            {
                List<_Teacher> teachers = new List<_Teacher>();
                foreach (_Course c in Courses)
                {
                    foreach (_Teacher t in c.Teachers)
                    {
                        _Teacher hasTeacher = teachers.Find(teacher => teacher.Id == t.Id);
                        if (hasTeacher==null)
                        {
                            teachers.Add(t);
                        }
                    }
                }
                return teachers;
            }
        }


        public List<_Class> Classes
        {
            get
            {
                List<_Class> classes = new List<_Class>();
                foreach (_Course c in Courses)
                {
                    foreach (_Class cl in c.Classes)
                    {
                        _Class hasClass = classes.Find(clazz => clazz.Id == cl.Id);
                        if (hasClass == null)
                        {
                            classes.Add(cl);
                        }
                    }
                }
                return classes;
            }
        }

        public List<_Room> Rooms
        {
            get
            {
                List<_Room> rooms = new List<_Room>();
                foreach (_Course c in Courses)
                {
                    foreach (_Room r in c.Rooms)
                    {
                        _Room hasRoom = rooms.Find(room => room.Id == r.Id);
                        if (hasRoom == null)
                        {
                            rooms.Add(r);
                        }
                    }
                }
                return rooms;
            }
        }

         public bool is_on(int day, int slot) // currently not used. 
        {            
            foreach (var t in Teachers) if (!t.is_on(day, slot)) return false;
            if (Classes != null) foreach (var t in Classes)  if (!t.is_on(day, slot)) return false;
            if (Rooms != null) foreach (var t in Rooms)    if (!t.is_on(day, slot)) return false;
            
            return true;
        }


        public _Cluster() : base() // ofer changed to public
        {
            Courses = new List<_Course>();

        }
    }

    
    public class ModelBase
    {
        public string SchoolName { get; set; }
        public string InstiCode { get; set; }
        public string ScopeId{ get; set; }
        public string SolutionId{ get; set; }


        public  List<_Subject> subjects;
        public  List<_Class> classes;
        public  List<_Teacher> teachers;
        public  List<_Room> rooms;
        public  List<_Course> courses;
        public  List<_Cluster> clusters;
        

        public  List<_Course> fCourses => courses.FindAll(course => course.Course_Type == COURSE_TYPE_ENUM.F);
        public  List<_Course> sCourses => courses.FindAll(course => course.Course_Type == COURSE_TYPE_ENUM.S);
        public  List<_Course> pCourses => courses.FindAll(course => course.Course_Type == COURSE_TYPE_ENUM.P);

        public List<_Course> teacherCourses(_Teacher t)  => courses.FindAll(course => course.Teachers.Contains(t));
        public List<_Course> classCourses(_Class cl) => courses.FindAll(course => course.Classes.Contains(cl));

        private Connect connect;
        
        protected ModelBase()
        {
            subjects = new List<_Subject>();
            classes = new List<_Class>();
            teachers = new List<_Teacher>();
            rooms = new List<_Room>();
            courses = new List<_Course>();
            clusters = new List<_Cluster>();
            connect = new Connect();
        
        }

        public _Course getCourse(string id)
        {
            return courses.Find(x=> x.Id == id.ToString());
        }

        _Teacher getTeacher(string id)
        {
            return teachers.Find(x => x.Id == id.ToString());
        }

        _Class getClass(string id)
        {
            return classes.Find(x => x.Id == id.ToString());
        }

        _Room getRoom(string id)
        {
            return rooms.Find(x => x.Id == id.ToString());
        }

        _Cluster getCluster(string id)
        {
            return clusters.Find(x => x.Id == id.ToString());
        }

        _Subject addSubject(string id, string name)
        {
            _Subject subject = new _Subject() { Name = name, Id = id };
            subjects.Add(subject);
            return subject;
        }

        _Teacher addTeacher(string id,string name)
        {
            _Teacher t = new _Teacher() { Name = name , Id = id};
            teachers.Add(t);
            return t;
        }

        _Class addClass(string id, string name, _Teacher teacher)
        {
            _Class c = new _Class(teacher) { Id = id, Name = name };
            classes.Add(c);
            teacher.MyClass = c;
            return c;
        }

        _Room addRoom(string id, string name)
        {
            _Room r = new _Room() { Id = id, Name = name };
            rooms.Add(r);
            return r;
        }

        _Course addCourse(string id, string name, string course_type, int hours, int max_daily_hours)
        {
            if (course_type == "?") course_type = "P";
            _Course c = new _Course() {
                Id = id,
                Name = name,
                Hours = hours,
                Max_Daily_Hours = max_daily_hours,
                Course_Type = (COURSE_TYPE_ENUM)Enum.Parse(typeof(COURSE_TYPE_ENUM), course_type)
            };
            courses.Add(c);
            return c;
        }

        _Cluster addCluster(string id, string name)
        {
            _Cluster c = new _Cluster() { Id = id, Name = name };
            clusters.Add(c);
            return c;
        }
       



        void setIsOn(_ObjectWithTimeTable o, JToken jObject)
        {
            IList<JToken> jDays = jObject["isOn"].ToList();
            int iDay = 0;
            foreach (JToken jDay in jDays)
            {
                int iSlot = 0;
                foreach (JToken jSlot in jDay.ToList())
                {
                    if (iSlot == _ObjectWithTimeTable.MAX_HOUR) break; // in case we restrict hours more than in the input file
                    o.tt[iDay, iSlot] = (jSlot.ToString() == "1" || jSlot.ToString() == "true");
                    iSlot++;
                }
                iDay++;
            }
        }


        public void LoadFromJson(string jsonString)
        {
            JObject json = JObject.Parse(jsonString);
            SolutionId = json["solution_id"].ToString();

            JToken jSchool = json["school"];
            SchoolName = jSchool["school_name"].ToString();
            InstiCode = jSchool["insti_code"].ToString();
            ScopeId = jSchool["scope_id"].ToString();
            


            IList<JToken> jSubjects = json["subjects"].Children().ToList();
            foreach (JToken jSubject in jSubjects)
            {
                string id = jSubject["id"].ToString();
                string name = jSubject["name"].ToString();
                addSubject(id, name);
            }


            IList<JToken> jTeachers = json["teachers"].Children().ToList();
            foreach (JToken jTeacher in jTeachers)
            {
                string id = jTeacher["id"].ToString();
                string name = jTeacher["name"].ToString();
                _Teacher teacher = addTeacher(id, name);
                setIsOn(teacher, jTeacher);
            }

            IList<JToken> jClasses = json["classes"].Children().ToList();
            foreach (JToken jClass in jClasses)
            {
                string id = jClass["id"].ToString();
                string name = jClass["name"].ToString();
                string teacherId = jClass["teacher"].ToString();
                _Teacher teacher = teachers.Find(x => x.Id == teacherId);
                _Class clazz = addClass(id, name, teacher);
                setIsOn(clazz, jClass);
            }


            IList<JToken> jRooms = json["rooms"].Children().ToList();
            foreach (JToken jRoom in jRooms)
            {
                string id = jRoom["id"].ToString();
                string name = jRoom["name"].ToString();
                _Room room = addRoom(id, name);
                setIsOn(room, jRoom);
            }

            IList<JToken> jCourses = json["courses"].Children().ToList();
            bool warned_filter = false;
            foreach (JToken jCourse in jCourses)
            {
                string id = jCourse["id"].ToString();
                string name = jCourse["name"].ToString();
                               

                string subject = jCourse["subject"].ToString();
                string course_type = jCourse["course_type"].ToString();
                int max_daily_hours = Int32.Parse(jCourse["max_daily_hours"].ToString());
                int hours = Int32.Parse(jCourse["hours"].ToString());
                List<JToken> class_ids = jCourse["classes"].ToList();

                // filterring 
                /*************************************************/
                //if (!name.Contains("א1")) continue;
                if (course_type == "P" || course_type == "?") continue; // ? = temporary
                if (name.Contains("צוות"))
                {
                    GlobalVar.Log.WriteLine("skipping " + name);
                    continue;
                }

                // in 'p' and 's' courses there are no classes hence the 
                // filter below (the else part) would filter it. 
                if (Program.flag_filter)
                {
                    bool ok = false;
                    if (course_type != "F") ok = true;
                    else
                        class_ids.ForEach(class_id =>
                    {
                        _Class cl = classes.Find(clazz => clazz.Id == class_id.ToString());
                        if (
                                false
                             || cl.Name.Contains("א")
                             || cl.Name.Contains("ב")
                             || cl.Name.Contains("ג")
                             || cl.Name.Contains("ד")
                             || cl.Name.Contains("ה")
                             || cl.Name.Contains("ו")
                        //|| cl.Name.Contains("ו2")
                        //|| cl.Name.Contains("ו3")
                        //|| cl.Name.Contains("ו4")
                        //|| cl.Name.Contains("ו5")
                        )
                            ok = true;
                    });

                    if (!ok)
                    {
                         //if (!warned_filter) MessageBox.Show("Warning: population is filterred");
                        warned_filter = true;
                        continue;
                    }
                }

                List<JToken> teacher_ids = jCourse["teachers"].ToList();

                if (class_ids.Count == 0 && teacher_ids.Count <= 1) continue; // extension of "P"

                /*************************************************/

                _Course c = addCourse(id, name, course_type, hours, max_daily_hours);

                c.Subject = subjects.Find(x => x.Id == subject.ToString());
                                        
                
                List<JToken> room_ids = jCourse["rooms"].ToList();

                Debug.Assert(course_type == "F" || teacher_ids.Count <= 1 || class_ids.Count == 0);

                class_ids.ForEach(class_id =>
                {
                    _Class cl = classes.Find(clazz => clazz.Id == class_id.ToString());
                    if (cl != null)
                    {
                        c.Classes.Add(cl);
                    }

                });

                teacher_ids.ForEach(teacher_id =>
                {
                    _Teacher t = teachers.Find(teacher => teacher.Id == teacher_id.ToString());
                    if (t != null)
                    {
                        c.Teachers.Add(t);
                    }

                });

                room_ids.ForEach(room_id =>
                {
                    _Room r = rooms.Find(room => room.Id == room_id.ToString());
                    if (r != null)
                    {
                        c.Rooms.Add(r);
                    }
                });
            }

            IList<JToken> jClusters = json["clusters"].Children().ToList();
            foreach (JToken jCluster in jClusters)
            {
                string id = jCluster["id"].ToString();
                string name = jCluster["name"].ToString();
                List<JToken> course_ids = jCluster["courses"].ToList();
                _Cluster cluster = addCluster(id, name);
                course_ids.ForEach(course_id =>
                {
                    try
                    {
                        _Course c = getCourse(course_id.ToString());
                        c.Clusters.Add(cluster);
                        cluster.Courses.Add(c);
                    }
                    catch { } // for when I screen te courses. 

                });
            }




        }


        public void LoadFromFile(string fileName)
        {
            if (fileName != "")
            {
                using (StreamReader stm = new StreamReader(fileName))
                {
                    LoadFromJson(stm.ReadToEnd());

                    
                }
            } else
            {
                /* read data from JSON file */
                /* then .... */
                _Teacher
                    t1 = addTeacher("1", "myTeacher1"),
                    t2 = addTeacher("2", "myTeacher2"),
                    t3 = addTeacher("3", "ProTeacher1"),
                    t4 = addTeacher("4", "ProTeacher2");
                _Class
                    cl1 = new _Class(t1) { Name = "c1" },
                    cl2 = new _Class(t2) { Name = "c2" };
                t1.MyClass = cl1;
                t2.MyClass = cl2;


                // on constraints: 
                t1.tt[1, 1] = t1.tt[1, 2] = t1.tt[1, 3] = true;
                t2.tt[1, 3] = t2.tt[1,4] = true;

                cl1.tt[1, 1] = cl1.tt[1, 2] = cl1.tt[1, 3] = true;
                cl2.tt[1, 3] = cl2.tt[1, 4] = true;

                _Course c1 = new _Course();
                c1.Name = "Math";
                c1.Id = "1";
                c1.Hours = 1;
                c1.Classes = new List<_Class> { cl1 };
                c1.Teachers = new List<_Teacher> { t1 };

                _Course c2 = new _Course();
                c2.Name = "English";
                c2.Id = "2";
                c2.Hours = 2;
                c2.Classes = new List<_Class> { cl2 };
                c2.Teachers = new List<_Teacher> { t2 };

                courses.Add(c1);
                courses.Add(c2);

                _Cluster cl = addCluster("id_cl1", "cluster1");
                cl.Courses.Add(c1);
                cl.Courses.Add(c2);

                c1.Clusters.Add(cl);
                c2.Clusters.Add(cl);
            }
        }

        public void showHours()
        {
            /* Frontal Hours */
            int fTotalHours = fCourses.Sum(c => {
                if (c.Clusters.Count > 0) { return 0; }
                return c.Hours;
            });
            Console.WriteLine("F total hours:" + fTotalHours.ToString());

            int clusterTotalHours = clusters.Sum(c => { return c.Hours; });
            Console.WriteLine("Cluster total hours:" + clusterTotalHours.ToString());



            int cTotalIsOn = classes.Sum(cl => { return cl.ttTotal; });
            Console.WriteLine("classes (tt) hours:" + cTotalIsOn.ToString());
            Console.WriteLine("------------------------------");

            int fOverlapTotalHours = fCourses.Sum(c => {
                if (c.Clusters.Count > 0) { return 0; }
                return c.Hours - c.tHours;

            });
            Console.WriteLine("F teachers Overlap  hours (non clusters) :" + fOverlapTotalHours.ToString());
            Console.WriteLine("------------------------------");

            /**************************************************************************/

            int fTeacherHours = fCourses.Sum(c => { return c.tHours; });
            Console.WriteLine("F total (teacher) hours:" + fTeacherHours.ToString());

            int fClusterHours = clusters.Sum(cluster => { return cluster.tHours; });
            Console.WriteLine("Cluster (teacher) hours:" + fClusterHours.ToString());

            int sTeacherHours = sCourses.Sum(c => { return c.tHours; });
            Console.WriteLine("S total (teacher) hours:" + sTeacherHours.ToString());

            int pTeacherHours = pCourses.Sum(c => { return c.Hours; });
            Console.WriteLine("P total (teacher) hours:" + pTeacherHours.ToString());

            Console.WriteLine("------------------------------");

            Console.WriteLine("Total (teacher) hours:" + (fTeacherHours + fClusterHours + sTeacherHours + pTeacherHours).ToString());

            int tTotalIsOn = teachers.Sum(t => { return t.ttTotal; });
            Console.WriteLine("teachers (tt) hours:" + tTotalIsOn.ToString());
            Console.WriteLine("------------------------------");



        }

        public string Pad(string s, int len)
        {
            return s.PadLeft(len);
        }

        public string Reverse(string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

        public void showClusters()
        {
            clusters.ForEach(cluster =>
            {
                string name = Reverse(cluster.Name);
                Console.WriteLine("cluster:" + name + " hours:" + cluster.Hours.ToString());
                cluster.Courses.ForEach(course => { showCourse(course,4); });
            });
        }
            

        public void showTeachers()
        {
            teachers.ForEach(teacher =>
            {
                Console.WriteLine("Teacher:" + Reverse(teacher.Name.ToString()));
                courses.ForEach(course =>
                {
                    if (course.Teachers.Contains(teacher))
                    {
                        string classNames = Reverse(string.Join(",", course.Classes.Select(clazz => clazz.Name)));
                        Console.WriteLine("   Course:" + Reverse(course.Subject.Name) + " [" + course.Course_Type.ToString()+"]  "+
                            classNames + " Hours:"+course.Hours.ToString());
                    };
                });
                Console.WriteLine("");
            });

        }

        public void showCourse(_Course course,int indent=0)
        {
            string subjectName = Reverse(course.Subject.Name);
            string classNames = Reverse(string.Join(",", course.Classes.Select(clazz => clazz.Name)));
            string teacherNames = Reverse(string.Join(",", course.Teachers.Select(teacher => teacher.Name)));

            Console.WriteLine(Pad("", indent) + " Course:" + subjectName + " Hours:" + course.Hours.ToString());
            Console.WriteLine(Pad("", indent) + "    Classes:" + classNames);
            Console.WriteLine(Pad("", indent) + "    Teachers:" + teacherNames);
        }

        public void showClasses()
        {
            classes.ForEach(clazz =>
            {
                Console.WriteLine("Class:" + Reverse(clazz.Name.ToString()) + " [" + Reverse(clazz.myTeacher.Name) + "]");
                
                courses.ForEach(course =>
                {
                    if (course.Classes.Contains(clazz))
                    {
                        showCourse(course);
                    }
                });
            });
        }


        public void showCourses(string label, List<_Course> theCourses)
        {
            Console.WriteLine(label);
            theCourses.ForEach(course =>{ showCourse(course,0);});
        }



        public void showCourses()
        {
            Console.WriteLine("fCourses" + " " + fCourses.Count.ToString());
            Console.WriteLine("sCourses" + " " + sCourses.Count.ToString());
            Console.WriteLine("pCourses" + " " + pCourses.Count.ToString());
            Console.WriteLine("------------------------------------------------");
            showCourses("F Courses:", fCourses);
            Console.WriteLine("------------------------------------------------");
            showCourses("S Courses:", sCourses);
            Console.WriteLine("------------------------------------------------");
            showCourses("P Courses:", pCourses);
            Console.WriteLine("------------------------------------------------");
            
        }

        void waitEnter()
        {
            Console.WriteLine("<Enter>");
            Console.ReadLine();
        }

        public void Monitor()
        {
            ConsoleKeyInfo key;
            do
            {
                Console.Clear();
                Console.WriteLine("=================================================");
                Console.WriteLine(Reverse(SchoolName) + " " + InstiCode + " " + ScopeId);
                Console.WriteLine("=================================================");
                
                Console.WriteLine("1) Hours Sumary");
                Console.WriteLine("2) Courses By Classes");
                Console.WriteLine("3) Courses By Teachers");
                Console.WriteLine("4) Courses");
                Console.WriteLine("5) Clusters");
                Console.WriteLine("<Esc> Quit");

                Console.Write(":");
                key = Console.ReadKey(true);
                Console.Clear();
                switch (key.Key)
                {
                    case ConsoleKey.D1:
                        showHours();
                        waitEnter();
                        break;
                    case ConsoleKey.D2:
                        showClasses();
                        waitEnter();
                        break;
                    case ConsoleKey.D3:
                        showTeachers();
                        waitEnter();
                        break;
                    case ConsoleKey.D4:
                        showCourses();
                        waitEnter();
                        break;
                    case ConsoleKey.D5:
                        showClusters();
                        waitEnter();
                        break;
                }
                
            }
            while (key.Key != ConsoleKey.Escape);
            Console.WriteLine("Bye...");
        }


        

        class Solution {
            public bool isFinal { get; set; }
            public string solution_id {get; set; }
            public List<SolutionLine> courses { get; set; }
            public Solution(string SolutionId, bool final)
            {
                solution_id = SolutionId;
                isFinal = final;
                courses = new List<SolutionLine>();
            }
        }


        public string SaveSolution(bool final)
        {
            Solution solution = new Solution(SolutionId, final);
            courses.ForEach(course =>
            {
                course.Solution.ForEach(sl =>
                {
                    solution.courses.Add(new SolutionLine(sl.group_id, sl.day, sl.slot));
                });
                course.Solution.Clear(); //  because we are submitting interm. solutions.

            });

            string postData = JsonConvert.SerializeObject(solution);
            return connect.Http("solution/save", postData);
            
        }
    }
}

    internal class Array<T>
    {

}   
