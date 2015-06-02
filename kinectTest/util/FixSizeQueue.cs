using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace votragsfinger2Back.util
{
    class FixSizeQueue<T> : Queue<T>
    {
        public FixSizeQueue(int size)
        {
            Size = size;
        }

        public int Size { get; set; }
        public new void Enqueue(T obj)
        {
            base.Enqueue(obj);
            while (base.Count > Size)
                base.Dequeue();
        }


        public new string ToString()
        {
            T[] arr = base.ToArray();
            string s = "";
            for (int i = 0; i < arr.Length; i++)
            {
                s += arr[i].ToString() + " ";
            }

            return s;
        }
    }
}
