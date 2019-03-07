using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Robot {

    //Controls and coordinates all the activity
    class Controller {

        public MainData data;

        Timer tmr_check;

        public Controller(ref MainData main_data) {

            data = main_data;
            data.NewWork += NewWork;

            data.workers = new List<WorkerThread>();
            }

        public void Initialize() {
            data.Log("->Inicializando Vinculos Originales");

            InitializeWorkers();
            data.Status = State.Working;

            int fcount = 0;
            for(int c = 0; c < data.org_links.Count; c++) {
                try {
                    data.Log("->VO: " + data.org_links[c]);
                    Page page = new Page(ref data, new URL(data.org_links[c]), c, true);
                    page.Proccess();

                    if(page.result.HasFlag(Result.Fail)) {
                        data.UpdateStatus(page, UrlStatus.Failed);
                        fcount++;
                        } else {
                        data.UpdateStatus(page, UrlStatus.ToDo);
                        }

                    } catch(Exception) {
                    data.AddFailed(data.org_links[c]);
                    }
                }

            if(fcount == data.org_links.Count) {
                Thread.Sleep(1000);
                data.Status = State.Iddle;
                }

            }

        void InitializeWorkers() {
            for(int c = 0; c < data.worker_count; c++) {
                data.workers.Add(new WorkerThread(c, ref data));
                }
            }

        int act_worker = 0;

        private void NewWork(Page work) {
            int act_works = data.workers[act_worker].works.Count;

            data.workers[act_worker].works.Enqueue(work);

            if(data.workers[act_worker].state == State.Iddle) {
                data.workers[act_worker].Start();
                if(tmr_check == null)
                    Checker();
                }

            if(act_worker == data.workers.Count - 1) {
                act_worker = 0;
                } else {
                act_worker++;
                }
            }

        public void Pause() {
            data.Status = State.Pausing;
            Checker();
            foreach(WorkerThread worker in data.workers) {
                worker.Pause();
                }
            }

        void Checker() {
            tmr_check = new Timer(Check, null, 1000, 500);
            }


        void Check(object obj) {

            //Check Paused
            if(data.Status == State.Pausing) {
                List<WorkerThread> sw_worker = data.workers;
                foreach(WorkerThread worker in sw_worker) {
                    if(worker.state != State.Paused)
                        return;
                    }

                data.Status = State.Paused;
                tmr_check.Dispose();
                tmr_check = null;

                //Check Idle
                } else if(data.Status == State.Working) {

                List<WorkerThread> sw_worker = data.workers;
                foreach(WorkerThread worker in sw_worker) {
                    if(worker.works.Count > 0 || data.links_todo.Count > 0)
                        return;
                    }

                string result = "";
                foreach(Page page in data.links_saved) {
                    result += page.final_url.str + "\r\n";
                    }

                File.WriteAllText("D:\\result.txt", result);

                data.Status = State.Iddle;
                tmr_check.Dispose();
                tmr_check = null;

                foreach(WorkerThread worker in data.workers) {
                    worker.Dispose();
                    }
                data.workers.Clear();
                
                }
            }

        public void Restart() {
            foreach(WorkerThread worker in data.workers) {
                worker.Resume();
                data.Status = State.Working;
                Checker();
                }
            }

        public void ForceClose() {
            foreach(WorkerThread wt in data.workers) {
                wt.ForceStop();
                }
            }


        }
    }
