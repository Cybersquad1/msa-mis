﻿using System;
using System.Drawing;
using System.Windows.Forms;

namespace MSAMISUserInterface {
    public partial class SchedViewDisReq : Form {
        public Shadow Refer;
        public MainForm Reference;

        public SchedViewDisReq() {
            InitializeComponent();
            Opacity = 0;
        }

        public int Rid { get; set; }

        private void Sched_ViewDisReq_Load(object sender, EventArgs e) {
            RefreshData();
            FadeTMR.Start();
        }

        private const int CsDropshadow = 0x20000;

        protected override CreateParams CreateParams {
            get {
                var cp = base.CreateParams;
                cp.ClassStyle |= CsDropshadow;
                return cp;
            }
        }
        private void RefreshData() {
            var dt = Scheduling.GetUnassignmentRequestDetails(Rid);
            ClientLBL.Text = dt.Rows[0][0].ToString();

            if (dt.Rows[0][1].ToString().Equals("Approved")) {
                ApproveBTN.Visible = false;
                DeclineBTN.Visible = false;
                NameLBL.Text = "Guards Unassigned";
                DateEffectiveLBL.Text = "Date Effective: " + dt.Rows[0]["dateeffective"]; 
                ApprovedByLBL.Text = "Approved by: " + dt.Rows[0]["uname"]; 
            }
            else if (dt.Rows[0][1].ToString().Equals("Pending")) {
                if (Login.AccountType != 2) { 
                ApproveBTN.Visible = true;
                DeclineBTN.Visible = true;
                }
                DateEffectiveLBL.Text = "Date Effective: " + dt.Rows[0]["dateeffective"]; 
                ApprovedByLBL.Text = "Approved by: " + dt.Rows[0]["uname"]; 
            }
            else {
                ApproveBTN.Visible = false;
                DeclineBTN.Visible = false;
                NameLBL.Text = "Declined Request to Unassign";
                DateEffectiveLBL.Visible = false;
                ApprovedByLBL.Text = "Declined by: " + dt.Rows[0]["uname"];
            }

            AssignedGRD.DataSource = Scheduling.GetGuardsToBeUnassigned(Rid);
            AssignedGRD.Columns[0].Visible = false;
            AssignedGRD.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            AssignedGRD.Columns[1].Width = 400;
            AssignedGRD.ColumnHeadersVisible = false;
        }

        private void FadeTMR_Tick(object sender, EventArgs e) {
            Opacity += 0.2;
            if (Opacity >= 1) FadeTMR.Stop();
        }

        private void Sched_ViewDisReq_FormClosing(object sender, FormClosingEventArgs e) {
            Refer.Hide();
        }

        private void CloseBTN_Click(object sender, EventArgs e) {
            Close();
        }

        private void ApproveBTN_Click(object sender, EventArgs e) {
            Scheduling.ApproveUnassignment(Rid);
            RefreshData();
            Reference.SchedLoadPage();
        }

        private void DeclineBTN_Click(object sender, EventArgs e) {
            Scheduling.DeclineRequest(Rid);
            ApproveBTN.Visible = false;
            DeclineBTN.Visible = false;
            RefreshData();
            Reference.SchedRefreshRequests();
        }

        private void ViewIncidentLBL_Click(object sender, EventArgs e) {
            try {
                var view = new SchedViewIncidentReport {
                    Rid = Rid,
                    Client = ClientLBL.Text,
                    Location = Location
                };
                view.ShowDialog();
            }
            catch (Exception) { }
        }
    }
}