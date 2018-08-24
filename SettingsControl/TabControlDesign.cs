
namespace System.Windows.Forms.Design {

    using System.Design;
    using System.Runtime.InteropServices;
    using System.ComponentModel;
    using System.Collections;
    using System.Diagnostics;
    using System;
    using System.ComponentModel.Design;
    using System.Drawing;
    using System.Windows.Forms;
    using Microsoft.Win32;
    using System.Drawing.Design;
    using System.Windows.Forms.Design.Behavior;

    ///  
    ///  
    ///      This designer handles the tab control.  It provides a design time way to add and
    ///      remove tabs as well as tab hit testing logic. 
    /// 
    internal class TabControlDesigner:ParentControlDesigner {

        private bool tabControlSelected = false;    //used for HitTest logic 
        private DesignerVerbCollection verbs;
        private DesignerVerb removeVerb;
        private bool disableDrawGrid = false;

        // Shadow SelectedIndex property so we separate the persisted value shown in the 
        // properties window from the visible selected page the user is currently working on.
        private int persistedSelectedIndex = 0;
        private bool addingOnInitialize;  //used so that we can fire the ComponentChangedEvent only aftger adding (both) the tabPages during
                                          // InitializeNewComponent. 

        private bool forwardOnDrag;     //should we forward the OnDrag call to the TabPageDesigner 

        /// 
        ///  
        ///     The TabControl will not re-parent any controls that are within it's lasso at
        ///     creation time.
        /// 
        protected override bool AllowControlLasso {
            get {
                return false;
                }
            }

        protected override bool DrawGrid {
            get {
                if(disableDrawGrid) {
                    return false;
                    }
                return base.DrawGrid;
                }
            }

        /// 
        /// 
        ///     Overridden to disallow SnapLines during drag operations if we are dragging TabPages around
        ///  
        public override bool ParticipatesWithSnapLines {
            get {
                if(!forwardOnDrag) {
                    return false;
                    } else {
                    TabPageDesigner pageDesigner = GetSelectedTabPageDesigner();
                    if(pageDesigner != null) {
                        return pageDesigner.ParticipatesWithSnapLines;
                        }

                    return true;
                    }
                }
            }

        /// 
        /// 
        ///     Accessor method for the SelectedIndex property on TabControl.  We shadow 
        ///     this property at design time.
        ///  
        private int SelectedIndex {
            get {
                return persistedSelectedIndex;
                }
            set {
                // TabBase.SelectedIndex has no validation logic, so neither do we
                persistedSelectedIndex = value;
                }
            }

        /// 
        ///  
        ///     Returns the design-time verbs supported by the component associated with
        ///     the customizer. The verbs returned by this method are typically displayed
        ///     in a right-click menu by the design-time environment. The return value may
        ///     be null if the component has no design-time verbs. When a user selects one 
        ///     of the verbs, the performVerb() method is invoked with the the
        ///     corresponding DesignerVerb object. 
        ///     NOTE: A design-time environment will typically provide a "Properties..." 
        ///     entry on a component's right-click menu. The getVerbs() method should
        ///     therefore not include such an entry in the returned list of verbs. 
        /// 
        public override  DesignerVerbCollection Verbs {
            get {
                if(verbs == null) {

                    removeVerb = new DesignerVerb(SR.GetString(SR.TabControlRemove), new EventHandler(this.OnRemove));

                    verbs = new DesignerVerbCollection();
                    verbs.Add(new DesignerVerb(SR.GetString(SR.TabControlAdd), new EventHandler(this.OnAdd)));
                    verbs.Add(removeVerb);
                    }
                if(Control != null) {
                    removeVerb.Enabled = Control.Controls.Count > 0;
                    }
                return verbs;
                }
            }

        /// 
        /// 
        ///     Called when the designer is intialized.  This allows the designer to provide some 
        ///     meaningful default values in the control.
        ///  
        public override void InitializeNewComponent(IDictionary defaultValues) {
            base.InitializeNewComponent(defaultValues);

            // Add 2 tab pages
            // member is OK to be null...
            //
            try {
                addingOnInitialize = true;
                this.OnAdd(this, EventArgs.Empty);
                this.OnAdd(this, EventArgs.Empty);
                } finally {
                addingOnInitialize = false;
                }

            MemberDescriptor member = TypeDescriptor.GetProperties(Component)["Controls"];
            RaiseComponentChanging(member);
            RaiseComponentChanged(member, null, null);


            TabControl tc = (TabControl) Component;
            if(tc != null) { //always Select the First Tab on Initialising the component...
                tc.SelectedIndex = 0;
                }

            }

        /// 
        ///  
        ///     Determines if the this designer can parent to the specified desinger --
        ///     generally this means if the control for this designer can parent the
        ///     given ControlDesigner's designer.
        ///  
        public override bool CanParent(Control control) {
            // If the tabcontrol already contains the control we are dropping then don't allow the drop. 
            // I.e. we don't want to allow local drag-drop for tabcontrols. 
            return ( control is TabPage && !this.Control.Contains(control) );
            }

        private void CheckVerbStatus() {
            if(removeVerb != null) {
                removeVerb.Enabled = Control.Controls.Count > 0;
                }
            }

        /// 
        ///  
        ///      This is the worker method of all CreateTool methods.  It is the only one
        ///      that can be overridden.
        /// 
        protected override IComponent[] CreateToolCore(ToolboxItem tool, int x, int y, int width, int height, bool hasLocation, bool hasSize) {
            TabControl tc = ( (TabControl) Control );
            //VSWhidbey #409457 
            if(tc.SelectedTab == null) {
                throw new ArgumentException(SR.GetString(SR.TabControlInvalidTabPageType, tool.DisplayName));
                }
            IDesignerHost host = (IDesignerHost) GetService(typeof(IDesignerHost));
            if(host != null) {
                TabPageDesigner selectedTabPageDesigner = (TabPageDesigner) host.GetDesigner(tc.SelectedTab);
                InvokeCreateTool(selectedTabPageDesigner, tool);
                }
            // InvokeCreate Tool will do the necessary hookups. 
            return null;
            }


        /// 
        /// 
        ///     Disposes of this designer. 
        /// 
        protected override void Dispose(bool disposing) {
            if(disposing) {
                ISelectionService svc = (ISelectionService) GetService(typeof(ISelectionService));
                if(svc != null) {
                    svc.SelectionChanged -= new EventHandler(this.OnSelectionChanged);
                    }

                IComponentChangeService cs = (IComponentChangeService) GetService(typeof(IComponentChangeService));
                if(cs != null) {
                    cs.ComponentChanged -= new ComponentChangedEventHandler(this.OnComponentChanged);
                    }
                TabControl tabControl = Control as TabControl;
                if(tabControl != null) {
                    tabControl.SelectedIndexChanged -= new EventHandler(this.OnTabSelectedIndexChanged);
                    tabControl.GotFocus -= new EventHandler(this.OnGotFocus);
                    tabControl.RightToLeftLayoutChanged -= new EventHandler(this.OnRightToLeftLayoutChanged);
                    tabControl.ControlAdded -= new ControlEventHandler(this.OnControlAdded);
                    }

                }

            base.Dispose(disposing);
            }

        /// 
        ///  
        ///     Allows your component to support a design time user interface.  A TabStrip 
        ///     control, for example, has a design time user interface that allows the user
        ///     to click the tabs to change tabs.  To implement this, TabStrip returns 
        ///     true whenever the given point is within its tabs.
        /// 
        protected override bool GetHitTest(Point point) {
            TabControl tc = ( (TabControl) Control );

            // tabControlSelected tells us if a tab page or the tab control itself is selected. 
            // If the tab control is selected, then we need to return true from here - so we can switch back and forth 
            // between tabs.  If we're not currently selected, we want to select the tab control
            // so return false. 
            if(tabControlSelected) {
                Point hitTest = Control.PointToClient(point);
                return !tc.DisplayRectangle.Contains(hitTest);
                }
            return false;
            }

        /// 
        ///  
        ///     Given a component, this retrieves the tab page that it's parented to, or
        ///     null if it's not parented to any tab page.
        /// 
        internal static TabPage GetTabPageOfComponent(object comp) {
            if(!( comp is Control )) {
                return null;
                }

            Control c = (Control) comp;
            while(c != null && !( c is TabPage )) {
                c = c.Parent;
                }
            return (TabPage) c;
            }

        ///  
        /// 
        ///     Called by the host when we're first initialized. 
        /// 
        public override void Initialize(IComponent component) {
            base.Initialize(component);

            AutoResizeHandles = true;
            TabControl control = component as TabControl;
            Debug.Assert(control != null, "Component must be a tab control, it is a: " + component.GetType().FullName);


            ISelectionService svc = (ISelectionService) GetService(typeof(ISelectionService));
            if(svc != null) {
                svc.SelectionChanged += new EventHandler(this.OnSelectionChanged);
                }

            IComponentChangeService cs = (IComponentChangeService) GetService(typeof(IComponentChangeService));
            if(cs != null) {
                cs.ComponentChanged += new ComponentChangedEventHandler(this.OnComponentChanged);
                }
            if(control != null) {
                control.SelectedIndexChanged += new EventHandler(this.OnTabSelectedIndexChanged);
                control.GotFocus += new EventHandler(this.OnGotFocus);
                control.RightToLeftLayoutChanged += new EventHandler(this.OnRightToLeftLayoutChanged);
                control.ControlAdded += new ControlEventHandler(this.OnControlAdded);
                }
            }


        /// 
        /// 
        ///      Called in response to a verb to add a tab.  This adds a new 
        ///      tab with a default name.
        ///  
        private void OnAdd(object sender, EventArgs eevent) {
            TabControl tc = (TabControl) Component;


            IDesignerHost host = (IDesignerHost) GetService(typeof(IDesignerHost));
            if(host != null) {

                DesignerTransaction t = null;
                try {
                    try {
                        t = host.CreateTransaction(SR.GetString(SR.TabControlAddTab, Component.Site.Name));
                        } catch(CheckoutException ex) {
                        if(ex == CheckoutException.Canceled) {
                            return;
                            }
                        throw ex;
                        }
                    MemberDescriptor member = TypeDescriptor.GetProperties(tc)["Controls"];

                    TabPage page = (TabPage) host.CreateComponent(typeof(TabPage));
                    if(!addingOnInitialize) {
                        RaiseComponentChanging(member);
                        }

                    // NOTE:  We also modify padding of TabPages added through the TabPageCollectionEditor.
                    //        If you need to change the default Padding, change it there as well. 
                    page.Padding = new Padding(3);

                    string pageText = null;

                    PropertyDescriptor nameProp = TypeDescriptor.GetProperties(page)["Name"];

                    if(nameProp != null && nameProp.PropertyType == typeof(string)) {
                        pageText = (string) nameProp.GetValue(page);
                        }

                    if(pageText != null) {
                        PropertyDescriptor textProperty = TypeDescriptor.GetProperties(page)["Text"];
                        Debug.Assert(textProperty != null, "Could not find 'Text' property in TabPage.");
                        if(textProperty != null) {
                            textProperty.SetValue(page, pageText);
                            }
                        }

                    PropertyDescriptor styleProp = TypeDescriptor.GetProperties(page)["UseVisualStyleBackColor"];
                    if(styleProp != null && styleProp.PropertyType == typeof(bool) && !styleProp.IsReadOnly && styleProp.IsBrowsable) {
                        styleProp.SetValue(page, true);
                        }

                    tc.Controls.Add(page);
                    // Make sure that the last tab is selected. 
                    tc.SelectedIndex = tc.TabCount - 1;
                    if(!addingOnInitialize) {
                        RaiseComponentChanged(member, null, null);
                        }


                    } finally {
                    if(t != null)
                        t.Commit();
                    }
                }
            }


        private void OnComponentChanged(object sender, ComponentChangedEventArgs e) {
            CheckVerbStatus();
            }


        private void OnGotFocus(object sender, EventArgs e) {
            IEventHandlerService eventSvc = (IEventHandlerService) GetService(typeof(IEventHandlerService));
            if(eventSvc != null) {
                Control focusWnd = eventSvc.FocusWindow;
                if(focusWnd != null) {
                    focusWnd.Focus();
                    }
                }
            }

        ///  
        /// 
        ///      This is called in response to a verb to remove a tab.  It removes
        ///      the current tab.
        ///  
        private void OnRemove(object sender, EventArgs eevent) {
            TabControl tc = (TabControl) Component;

            // if the control is null, or there are not tab pages, get out!...
            // 
            if(tc == null || tc.TabPages.Count == 0) {
                return;
                }

            // member is OK to be null...
            // 
            MemberDescriptor member = TypeDescriptor.GetProperties(Component)["Controls"];

            TabPage tp = tc.SelectedTab;

            // destroy the page
            //
            IDesignerHost host = (IDesignerHost) GetService(typeof(IDesignerHost));
            if(host != null) {
                DesignerTransaction t = null;
                try {
                    try {
                        t = host.CreateTransaction(SR.GetString(SR.TabControlRemoveTab, ( (IComponent) tp ).Site.Name, Component.Site.Name));
                        RaiseComponentChanging(member);
                        } catch(CheckoutException ex) {
                        if(ex == CheckoutException.Canceled) {
                            return;
                            }
                        throw ex;
                        }

                    host.DestroyComponent(tp);

                    RaiseComponentChanged(member, null, null);
                    } finally {
                    if(t != null)
                        t.Commit();
                    }
                }
            }


        protected override void OnPaintAdornments(PaintEventArgs pe) {
            try {
                this.disableDrawGrid = true;

                // we don't want to do this for the tab control designer
                // because you can't drag anything onto it anyway. 
                // so we will always return false for draw grid.
                base.OnPaintAdornments(pe);

                } finally {
                this.disableDrawGrid = false;
                }
            }

        private void OnControlAdded(object sender, ControlEventArgs e) {
            if(e.Control != null && !e.Control.IsHandleCreated) {
                IntPtr hwnd = e.Control.Handle;
                }
            }

        private void OnRightToLeftLayoutChanged(object sender, EventArgs e) {
            if(BehaviorService != null) {
                BehaviorService.SyncSelection();
                }
            }

        ///  
        /// 
        ///      Called when the current selection changes.  Here we check to 
        ///      see if the newly selected component is one of our tabs.  If it 
        ///      is, we make sure that the tab is the currently visible tab.
        ///  
        private void OnSelectionChanged(Object sender, EventArgs e) {
            ISelectionService svc = (ISelectionService) GetService(typeof(ISelectionService));

            tabControlSelected = false;//this is for HitTest purposes 

            if(svc != null) {
                ICollection selComponents = svc.GetSelectedComponents();

                TabControl tabControl = (TabControl) Component;

                foreach(object comp in selComponents) {

                    if(comp == tabControl) {
                        tabControlSelected = true;//this is for HitTest purposes
                        }

                    TabPage page = GetTabPageOfComponent(comp);

                    if(page != null && page.Parent == tabControl) {
                        tabControlSelected = false; //this is for HitTest purposes
                        tabControl.SelectedTab = page;
                        SelectionManager selMgr = (SelectionManager) GetService(typeof(SelectionManager));
                        selMgr.Refresh();
                        break;
                        }
                    }
                }
            }

        /// 
        ///  
        ///      Called when the selected tab changes.  This accesses the design
        ///      time selection service to surface the new tab as the current 
        ///      selection. 
        /// 
        private void OnTabSelectedIndexChanged(Object sender, EventArgs e) {

            // if this was called as a result of a prop change, don't set the
            // selection to the control (causes flicker)
            // 
            // Attempt to select the tab control
            ISelectionService svc = (ISelectionService) GetService(typeof(ISelectionService));
            if(svc != null) {
                ICollection selComponents = svc.GetSelectedComponents();

                TabControl tabControl = (TabControl) Component;
                bool selectedComponentOnTab = false;

                foreach(object comp in selComponents) {
                    TabPage page = GetTabPageOfComponent(comp);
                    if(page != null && page.Parent == tabControl && page == tabControl.SelectedTab) {
                        selectedComponentOnTab = true;
                        break;
                        }
                    }

                if(!selectedComponentOnTab) {
                    svc.SetSelectedComponents(new Object[] { Component });
                    }
                }
            }

        protected override void PreFilterProperties(IDictionary properties) {
            base.PreFilterProperties(properties);

            // Handle shadowed properties
            // 
            string[] shadowProps = new string[] {
                "SelectedIndex",
            };

            Attribute[] empty = new Attribute[0];

            for(int i = 0; i < shadowProps.Length; i++) {
                PropertyDescriptor prop = (PropertyDescriptor) properties[shadowProps[i]];
                if(prop != null) {
                    properties[shadowProps[i]] = TypeDescriptor.CreateProperty(typeof(TabControlDesigner), prop, empty);
                    }
                }
            }

        private TabPageDesigner GetSelectedTabPageDesigner() {
            TabPageDesigner pageDesigner = null;
            TabPage selectedTab = ( (TabControl) Component ).SelectedTab;
            if(selectedTab != null) {
                IDesignerHost host = (IDesignerHost) GetService(typeof(IDesignerHost));
                if(host != null) {
                    pageDesigner = host.GetDesigner(selectedTab) as TabPageDesigner;
                    }
                }
            return pageDesigner;
            }

        ///  
        /// 
        ///      Called in response to a drag enter for OLE drag and drop.  This method is overriden 
        ///      so that we can forward the OnDragEnter event to the currently selected TabPage. 
        /// 
        protected override void OnDragEnter(DragEventArgs de) {

            // Check what we are dragging... If we are just dragging tab pages,
            // then we do not want to forward the OnDragXXX

            forwardOnDrag = false;

            DropSourceBehavior.BehaviorDataObject data = de.Data as DropSourceBehavior.BehaviorDataObject;
            if(data != null) {
                ArrayList dragControls;
                int primaryIndex = -1;
                dragControls = data.GetSortedDragControls(ref primaryIndex);
                if(dragControls != null) {
                    for(int i = 0; i < dragControls.Count; i++) {
                        if(!( dragControls[i] is Control ) || ( dragControls[i] is Control && !( dragControls[i] is TabPage ) )) {
                            forwardOnDrag = true;
                            break;
                            }
                        }

                    }
                } else {
                // We must be dragging something off the toolbox, so forward the drag to the right tabpage.
                forwardOnDrag = true;
                }

            if(forwardOnDrag) {
                TabPageDesigner pageDesigner = GetSelectedTabPageDesigner();
                if(pageDesigner != null) {
                    pageDesigner.OnDragEnterInternal(de);
                    }
                } else {
                base.OnDragEnter(de);
                }
            }

        /// 
        /// 
        ///      Called in response to a drag enter for OLE drag and drop.  This method is overriden 
        ///      so that we can forward the OnDragEnter event to the currently selected TabPage.
        ///  
        protected override void OnDragDrop(DragEventArgs de) {
            if(forwardOnDrag) {
                TabPageDesigner pageDesigner = GetSelectedTabPageDesigner();
                if(pageDesigner != null) {
                    pageDesigner.OnDragDropInternal(de);
                    }
                } else {
                base.OnDragDrop(de);
                }

            forwardOnDrag = false;
            }

        /// 
        ///  
        ///      Called in response to a drag leave for OLE drag and drop.  This method is overriden
        ///      so that we can forward the OnDragLeave event to the currently selected TabPage. 
        ///  
        protected override void OnDragLeave(EventArgs e) {
            if(forwardOnDrag) {
                TabPageDesigner pageDesigner = GetSelectedTabPageDesigner();
                if(pageDesigner != null) {
                    pageDesigner.OnDragLeaveInternal(e);
                    }
                } else {
                base.OnDragLeave(e);
                }

            forwardOnDrag = false;
            }

        ///  
        /// 
        ///      Called in response to a drag over for OLE drag and drop.  This method is overriden 
        ///      so that we can forward the OnDragOver event to the currently selected TabPage. 
        /// 
        protected override void OnDragOver(DragEventArgs de) {
            if(forwardOnDrag) {
                //Need to make sure that we are over a valid area.
                //VSWhidbey# 354139. Now that all dragging/dropping is done via
                //the behavior service and adorner window, we have to do our own 
                //validation, and cannot rely on the OS to do it for us.
                TabControl tc = ( (TabControl) Control );
                Point dropPoint = Control.PointToClient(new Point(de.X, de.Y));
                if(!tc.DisplayRectangle.Contains(dropPoint)) {
                    de.Effect = DragDropEffects.None;
                    return;
                    }

                TabPageDesigner pageDesigner = GetSelectedTabPageDesigner();
                if(pageDesigner != null) {
                    pageDesigner.OnDragOverInternal(de);
                    }
                } else {
                base.OnDragOver(de);
                }
            }

        /// 
        ///  
        ///      Called in response to a give feedback for OLE drag and drop.  This method is overriden 
        ///      so that we can forward the OnGiveFeedback event to the currently selected TabPage.
        ///  
        protected override void OnGiveFeedback(GiveFeedbackEventArgs e) {
            if(forwardOnDrag) {
                TabPageDesigner pageDesigner = GetSelectedTabPageDesigner();
                if(pageDesigner != null) {
                    pageDesigner.OnGiveFeedbackInternal(e);
                    }
                } else {
                base.OnGiveFeedback(e);
                }
            }

        ///  
        /// 
        ///      Overrides control designer's wnd proc to handle HTTRANSPARENT. 
        ///  
        protected override void WndProc(ref Message m) {
            switch(m.Msg) {
                case NativeMethods.WM_NCHITTEST:
                    // The tab control always fires HTTRANSPARENT in empty areas, which
                    // causes the message to go to our parent.  We want
                    // the tab control's designer to get these messages, however, 
                    // so change this.
                    // 
                    base.WndProc(ref m);
                    if((int) m.Result == NativeMethods.HTTRANSPARENT) {
                        m.Result = (IntPtr) NativeMethods.HTCLIENT;
                        }
                    break;
                case NativeMethods.WM_CONTEXTMENU:
                    // We handle this in addition to a right mouse button. 
                    // Why?  Because we often eat the right mouse button, so
                    // it may never generate a WM_CONTEXTMENU.  However, the 
                    // system may generate one in response to an F-10. 
                    //
                    int x = NativeMethods.Util.SignedLOWORD((int) m.LParam);
                    int y = NativeMethods.Util.SignedHIWORD((int) m.LParam);
                    if(x == -1 && y == -1) {
                        // for shift-F10
                        Point p = Cursor.Position;
                        x = p.X;
                        y = p.Y;
                        }
                    OnContextMenu(x, y);
                    break;
                case NativeMethods.WM_HSCROLL:
                case NativeMethods.WM_VSCROLL:
                    //We do this so that we can update the areas covered by glyphs correctly. VSWhidbey# 187405.
                    //We just invalidate the area corresponding to the ClientRectangle in the adornerwindow. 
                    BehaviorService.Invalidate(BehaviorService.ControlRectInAdornerWindow(Control));
                    base.WndProc(ref m);
                    break;
                default:
                    base.WndProc(ref m);
                    break;
                }
            }
        }
    }


// File provided for Reference Use Only by Microsoft Corporation (c) 2007.
// Copyright (c) Microsoft Corporation. All rights reserved.