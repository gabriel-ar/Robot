using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Robot {
    class WorkerThread:IDisposable {
        int id;
        public Thread thread;
        MainData data;
        public State state;
        public Queue<Page> works;
        public string act_work;

        public WorkerThread(int id, ref MainData main_data) {
            this.id = id;
            data = main_data;

            state = State.Iddle;
            thread = new Thread(new ThreadStart(Work));
            thread.Name = "ROBOT_WORKER_" + id.ToString();
            works = new Queue<Page>();
            }

        public void Start() {
            state = State.Working;
            if(thread.ThreadState == ThreadState.Unstarted)
                thread.Start();
            }

        public void Resume() {
            state = State.Working;
            thread.Interrupt();
            }

        public void Pause() {
            state = State.Pausing;
            }

        public void ForceStop() {
            try {
                thread.Abort();
                } catch {
                return;
                }
            }

        void Work() {

            while(true) {
                if(state == State.Working) {

                    if(works.Count > 0) {
                        Page pg = works.Dequeue();

                        act_work = pg.final_url.str;
                        Crawler pp = new Crawler(ref data, pg);
                        } else {
                        Thread.Sleep(100);
                        continue;
                        }

                    } else if(state == State.Pausing) {
                    state = State.Paused;
                    try {
                        Thread.CurrentThread.Join();
                        } catch(Exception) {
                        data.Log2("Deteniendo " + thread.Name);
                        }
                    }
                }

            }//Work

        public void Dispose() {
            thread.Abort();
            thread = null;
            works.Clear();
            works = null;
            }
        }

    }
