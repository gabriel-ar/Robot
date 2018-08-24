using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace Robot.SettingsControl {
    class SettingsControlDesign:ParentControlDesigner {

        DesignerVerb remove_verb;
        DesignerVerbCollection verb_coll;

        bool selected = false;

        public override DesignerVerbCollection Verbs {
            get {
                if(verb_coll == null) {
                    remove_verb = new DesignerVerb("Remove Selected Section", RemoveSection);

                    verb_coll = new DesignerVerbCollection();
                    verb_coll.Add(new DesignerVerb("Edit Sections", EditSections));
                    verb_coll.Add(new DesignerVerb("Add Section", AddSection));
                    verb_coll.Add(remove_verb);

                    } else if(Control != null) {

                    remove_verb.Enabled = Control.Controls.Count > 0;
                    }
                return verb_coll;
                }
            }

        public override void Initialize(IComponent component) {
            base.Initialize(component);

            ISelectionService svc = (ISelectionService) GetService(typeof(ISelectionService));
            if(svc != null) {
                svc.SelectionChanged += SelectionChanged;
                }
            }

        public override void InitializeNewComponent(IDictionary defaultValues) {
            base.InitializeNewComponent(defaultValues);

            AddSection(this, EventArgs.Empty);
            AddSection(this, EventArgs.Empty);


            }

        void EditSections(object obj, EventArgs e) {
            /*
            SettingsControl sc = Control as SettingsControl;

            SettingsEditor se = new SettingsEditor(typeof(SettingsItem), sc.Items);
            se.Show();            */
            }

        public void AddSection(object obj, EventArgs e) {
            SettingsControl sc = (SettingsControl) Component;

            IDesignerHost host = (IDesignerHost) GetService(typeof(IDesignerHost));
            if(host == null)
                return;

            try {
                SettingsItem item = (SettingsItem) host.CreateComponent(typeof(SettingsItem));
                item.TabText = "Settings Item";

                sc.Items.Add(item);
                sc.UpdateList();

                if(sc.Items.Count == 1)
                    sc.ShowItem(0);

                } catch {

                }

            remove_verb.Enabled = sc.Items.Count > 0;
            }

        public void RemoveSection(object obj, EventArgs e) {
            SettingsControl sc = (SettingsControl) Control;
            IDesignerHost host = (IDesignerHost) GetService(typeof(IDesignerHost));


            if(sc.Items.Count > 0 && sc.selected_item != null) {
                int index = sc.selected_item.index;
                int sc_count = sc.Items.Count;

                host.DestroyComponent(sc.selected_item);

                sc.Items.RemoveAt(index);

                if(index + 1 == sc_count && sc.Items.Count > 0) {
                    sc.ShowItem(sc.Items.Count - 1);
                    }
                sc.UpdateList();
                }

            remove_verb.Enabled = sc.Items.Count > 0;
            }

        void ReloadTexts() {
            SettingsControl sc = (SettingsControl) Control;
            sc.UpdateList();
            }

        private void SelectionChanged(object sender, EventArgs e) {
            ISelectionService svc = (ISelectionService) GetService(typeof(ISelectionService));

            SettingsControl sc = (SettingsControl) Component;

            foreach(object comp in svc.GetSelectedComponents()) {
                if(comp == sc) {
                    selected = true;
                    ReloadTexts();
                    return;
                    }
                }
            
            foreach(SettingsItem item in sc.Items) {
                foreach(Control ctrl in svc.GetSelectedComponents()) {
                    if(item.Contains(ctrl) && sc.selected_item != item) {
                        sc.ShowItem(item.index);
                        break;
                        }
                    }
                }
            
            selected = false;
            }

        protected override bool GetHitTest(Point point) {
            return selected;
            }

        //Elimina los hijos asociados a SplitterPanel
        protected override void Dispose(bool disposing) {
            if(disposing)
                try {

                    ISelectionService svc = (ISelectionService) GetService(typeof(ISelectionService));
                    if(svc != null) {
                        svc.SelectionChanged -= SelectionChanged;
                        }

                    SettingsControl sc = (SettingsControl) Control;
                    IDesignerHost host = (IDesignerHost) GetService(typeof(IDesignerHost));

                    foreach(SettingsItem itm in sc.Items) {
                        host.DestroyComponent(itm);
                        }
                    } catch {
                    }

            base.Dispose(disposing);
            }

        }
    }
