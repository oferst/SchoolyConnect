using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;


namespace SchoolyConnect
{

    enum COURSE_TYPE_ENUM { F=0,S=1,P=2};

    class _Object
    {
        
        private string id = "";
        private string name = "";

        public string Id { get => id; set => id = value; }
        public string Name { get => name; set => name = value; }
        
        protected _Object()
        {
            
        }
    }
    
    class _ObjectWithTimeTable : _Object
    {
        public const int MAX_DAY = 6;
        public const int MAX_HOUR = 10;

        protected _ObjectWithTimeTable () : base()
        {
         
        }
            
        public bool[,] tt = new bool[MAX_DAY, MAX_HOUR]; // ofer changed to public

        public bool is_on (int day,int slot)
        {
            return tt[day, slot];
        }
    }



    class _Room : _ObjectWithTimeTable
    {
        public _Room() : base()
        {

        }

    }

    class _Teacher : _ObjectWithTimeTable
    {
        private _Class myClass;

        public _Class MyClass { get => myClass; set => myClass = value; }
                

        public _Teacher() : base() // ofer changed to public
        {
            
        }
    }

    class _Class : _ObjectWithTimeTable
    {
        protected _Teacher myTeacher;

        public _Class (_Teacher t) : base() // ofer changed to public
        {
            myTeacher = t;            
        }
    }



    class _Course : _Object
    {
        public int Hours { get; set; }

        public  COURSE_TYPE_ENUM  Course_Type { get; set; }
        public int  Max_Daily_Hours { get; set; }
        public List<_Teacher> Teachers { get; set; }
        public  List<_Class> Classes { get; set; }
        public List<_Room> Rooms { get; set; }


        public _Course() : base()
        {

        }

        public _Course(_Course c) : base()
        {
            var props = typeof(_Course).GetProperties().Where(x => x.CanRead).ToList();
            foreach (var p in props)
            {
                p.SetValue(this, p.GetValue(c, null), null);
            }
        }
        
        public bool is_on(int day, int slot) 
        {            
            foreach (var t in Teachers) if (!t.is_on(day, slot)) return false;
            if (Classes != null) foreach (var t in Classes)  if (!t.is_on(day, slot)) return false;
            if (Rooms != null) foreach (var t in Rooms)    if (!t.is_on(day, slot)) return false;
            
            return true;
        }
    }

    
    class ModelBase
    {
        public  List<_Class> classes;
        public  List<_Teacher> teachers;
        public  List<_Room> rooms;
        public  List<_Course> courses;

        protected ModelBase()
        {
            classes = new List<_Class>();
            teachers = new List<_Teacher>();
            rooms = new List<_Room>();
            courses = new List<_Course>();
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

        void setIsOn(_ObjectWithTimeTable o, JToken jObject)
        {
            IList<JToken> jDays = jObject["is_on"].ToList();
            int iDay = 0;
            foreach (JToken jDay in jDays)
            {
                int iSlot = 0;
                foreach (JToken jSlot in jDay.ToList())
                {
                    o.tt[iDay, iSlot] = (jSlot.ToString() == "1" || jSlot.ToString() == "true");
                    iSlot++;
                }
                iDay++;
            }
        }


        public void Load(string fileName)
        {
            if (fileName != "")
            {
                using (StreamReader stm = new StreamReader(fileName))
                {
                    JObject json = JObject.Parse(stm.ReadToEnd());

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
                    foreach (JToken jCourse in jCourses)
                    {
                        string id = jCourse["id"].ToString();
                        string name = jCourse["name"].ToString();
                        string course_type = jCourse["course_type"].ToString();
                        int max_daily_hours = Int32.Parse(jCourse["max_daily_hours"].ToString());
                        int hours = Int32.Parse(jCourse["hours"].ToString());
                        _Course c = addCourse(id, name, course_type, hours, max_daily_hours);

                        List<JToken> class_ids = jCourse["classes"].ToList();
                        List<JToken> teacher_ids = jCourse["teachers"].ToList();
                        List<JToken> room_ids = jCourse["rooms"].ToList();

                        class_ids.ForEach(class_id =>
                        {
                            _Class cl = classes.Find(clazz => clazz.Id == id.ToString());
                            c.Classes.Add(cl);
                        });

                        teacher_ids.ForEach(teacher_id =>
                        {
                            _Teacher t = teachers.Find(teacher => teacher.Id == teacher_id.ToString());
                            c.Teachers.Add(t);
                        });

                        room_ids.ForEach(room_id =>
                        {
                            _Room r = rooms.Find(room => room.Id == room_id.ToString());
                            c.Rooms.Add(r);
                        });
                    }
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
                //t2.tt[3, 2] = t3.tt[1, 1] = t1.tt[1, 1] = true;
                //cl1.tt[3, 2] = cl1.tt[1, 1] = true;


                courses.Add(new _Course()
                {
                    Name = "Math",
                    Id = "1",
                    Hours = 5,
                    Classes = new List<_Class> { cl1, cl2 },
                    Teachers = new List<_Teacher> { t1, t3, t4 }
                });
                courses.Add(new _Course()
                {
                    Name = "English",
                    Id = "2",
                    Hours = 3,
                    Classes = new List<_Class> { cl1 },
                    Teachers = new List<_Teacher> { t1, t3 }
                });
            }
        }

        public void SaveSolution()
        {
            Console.WriteLine("Thank You! ");
        
        }

    }
}

