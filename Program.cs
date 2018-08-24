using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Robot {
    static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// 
        [Flags]
        enum AST {
            Stop = 1,
            Pause = 2,
            Play = 3,
            Iddle = 4
            }


        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Main());
            }
        }
    }
