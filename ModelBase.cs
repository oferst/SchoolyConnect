using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchoolyConnect
{
    enum COURSE_TYPE_ENUM { F, S, P };

   

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
        public const int MAX_SLOT = 10;

        protected _ObjectWithTimeTable () : base()
        {
         
        }
            
        public bool[,] tt = new bool[MAX_DAY, MAX_SLOT]; // ofer changed to public

        public bool is_on (int day,int slot)
        {
            return tt[day, slot];
        }
    }



    class _Room : _ObjectWithTimeTable
    {
        protected _Room() : base()
        {

        }

    }

    class _Teacher : _ObjectWithTimeTable
    {
        private _Class myClass;

        public _Class MyClass { get => myClass; set => myClass = value; }
                

        public _Teacher(_Class c) : base() // ofer changed to public
        {
            MyClass = c;            
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
        
        public bool is_on(int day, int slot) // ofer changed to public
        {
            bool result = true;
            Teachers.ForEach(delegate (_Teacher t)
            {
                result = result && t.is_on(day, slot);
            });
            if (result)
            {
                Classes.ForEach(delegate (_Class c)
                {
                    result = result && c.is_on(day, slot);
                });
            }
            if (result)
            {
                Rooms?.ForEach(delegate (_Room r)
                {
                    result = result && r.is_on(day, slot);
                });
            }
            return result;
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
        
        _Teacher addTeacher(_Class cl, string name)
        {
            _Teacher t = new _Teacher(cl) { Name = name };
            teachers.Add(t);
            return t;
        }

        public void Load(string fileName)
        {
            /* read data from JSON file */
            /* then .... */            
            _Teacher 
                t1 = addTeacher(null,"myTeacher1"), 
                t2 = addTeacher(null,"myTeacher2"),
                t3 = addTeacher(null,"ProTeacher1"),
                t4 = addTeacher(null,"ProTeacher2");            
            _Class 
                cl1 = new _Class(t1) { Name = "c1" }, 
                cl2 = new _Class(t2) { Name = "c2" };
            t1.MyClass = cl1;
            t2.MyClass = cl2;
           
            // on constraints: 
            t2.tt[3,2] = t3.tt[1, 1] = true; 
            cl1.tt[3, 2] = cl1.tt[1, 1] = true; 
                        

            courses.Add(new _Course() {
                Name = "Math",
                Hours = 5,
                Classes = new List<_Class> { cl1, cl2 },
                Teachers = new List<_Teacher> { t3, t4}
            });
            courses.Add(new _Course()
            {
                Name = "English",
                Hours = 3,
                Classes = new List<_Class> { cl1 },
                Teachers = new List<_Teacher> { t3 }
            });
        }

        public void SaveSolution()
        {
            Console.WriteLine("Thank You! ");
            

        }

    }
}
