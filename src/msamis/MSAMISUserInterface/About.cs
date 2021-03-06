﻿using System;
using System.Drawing;
using System.Windows.Forms;
using rylui;

namespace MSAMISUserInterface {
    public partial class About : Form {
        private readonly Color _chose = Color.FromArgb(53, 64, 82);
        private readonly Color _def = Color.Gray;
        private Label _currentLbl;
        private Panel _currentPnl;
        public Shadow Refer;

        public About() {
            InitializeComponent();
            Opacity = 0;
        }

        public string Username { get; set; }

        private const int CsDropshadow = 0x20000;

        protected override CreateParams CreateParams {
            get {
                var cp = base.CreateParams;
                cp.ClassStyle |= CsDropshadow;
                return cp;
            }
        }

        private void About_Load(object sender, EventArgs e) {
            FadeTMR.Start();
            _currentLbl = AboutLBL;
            _currentPnl = AboutPNL;

            UsersPNL.Visible = false;
        }

        private void About_FormClosing(object sender, FormClosingEventArgs e) {
            if (CloseBTN.Visible) Refer.Close();
            else e.Cancel = true;
        }

        private void FadeTMR_Tick(object sender, EventArgs e) {
            Opacity += 0.2;
            if (Opacity >= 1) FadeTMR.Stop();
        }

        private void CloseBTN_Click(object sender, EventArgs e) {
            Close();
            Refer.Hide();
        }

        private void ChangePanel(Label newL, Panel newP) {
            _currentLbl.ForeColor = _def;
            _currentPnl.Visible = false;
            newL.ForeColor = _chose;
            newP.Visible = true;
            _currentLbl = newL;
            _currentPnl = newP;
        }

        private void AboutLBL_Click(object sender, EventArgs e) {
            ChangePanel(AboutLBL, AboutPNL);
        }

        private void UsersLBL_Click(object sender, EventArgs e) {
            ChangePanel(UsersLBL, UsersPNL);
            if (Login.AccountType == 2) {
                EditUserPNL.Visible = UsersGRDPNL.Visible = false;
            } else LoadUsers();
        }

        private void UsersLBL_MouseEnter(object sender, EventArgs e) {
            UsersLBL.ForeColor = _chose;
        }

        private void AboutLBL_MouseEnter(object sender, EventArgs e) {
            AboutLBL.ForeColor = _chose;
        }

        private void AboutLBL_MouseLeave(object sender, EventArgs e) {
            if (!AboutPNL.Visible) AboutLBL.ForeColor = _def;
        }

        private void UsersLBL_MouseLeave(object sender, EventArgs e) {
            if (!UsersPNL.Visible) UsersLBL.ForeColor = _def;
        }

        private void About_FormClosed(object sender, FormClosedEventArgs e) { }

        private void LoadUsers() {
            UsersGRDPNL.Visible = true;
            UsersGRDPNL.BringToFront();
            UsersGRD.DataSource = Account.GetAccounts();
            UsersGRD.Columns[0].Visible = false;
            UsersGRD.Columns[1].HeaderText = "USERNAME";
            UsersGRD.Columns[1].Width = 150;
            UsersGRD.Columns[2].HeaderText = "ACCOUNT TYPE";
            UsersGRD.Columns[2].Width = 150;
        }

        private void EditBTN_Click(object sender, EventArgs e) {
            UsernameBX.Text = UsersGRD.SelectedRows[0].Cells[1].Value.ToString();
            CurrentBX.Clear();
            CurrentBX.Enabled = true;
            CurrentPassPic.Visible = true;
            NewBX.Clear();
            UsersGRDPNL.Visible = false;
            EditUserPNL.Visible = true;
            CloseBTN.Visible = false;
            AdminRDBTN.Checked = UsersGRD.SelectedRows[0].Cells[2].Value.ToString().Equals("Manager");
            ClerkRDBTN.Checked = UsersGRD.SelectedRows[0].Cells[2].Value.ToString().Equals("Clerk");
            AdminRDBTN.Enabled = false;
            ClerkRDBTN.Enabled = false;
            AboutLBL.Visible = false;
            SaveBTN.Text = "SAVE";
        }

        private void SaveBTN_Click(object sender, EventArgs e) {
            if (SaveBTN.Text.Equals("SAVE")) {
                if (Account.ChangePassword(int.Parse(UsersGRD.SelectedRows[0].Cells[0].Value.ToString()), NewBX.Text.Replace("'", string.Empty),
                    CurrentBX.Text.Replace("'", string.Empty))) {
                    Account.ChangeUsername(int.Parse(UsersGRD.SelectedRows[0].Cells[0].Value.ToString()),
                        UsernameBX.Text.Replace("'", string.Empty));
                    LoadUsers();
                    CancelBTN.PerformClick();
                    RylMessageBox.ShowDialog("Account Details Changed", "Accounts Details",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else {
                    RylMessageBox.ShowDialog("Current Password is incorrect", "Accounts Details",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    NewBX.Clear();
                    CurrentBX.Clear();
                }
            }
            else {
                Account.CreateUser(UsernameBX.Text.Replace("'", string.Empty), NewBX.Text.Replace("'", string.Empty), AdminRDBTN.Checked ? 1 : 2);
                LoadUsers();
                CancelBTN.PerformClick();
                RylMessageBox.ShowDialog("User was added", "Users",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void CancelBTN_Click(object sender, EventArgs e) {
            UsersGRDPNL.Visible = true;
            EditUserPNL.Visible = false;
            CloseBTN.Visible = true;
            AboutLBL.Visible = true;
        }

        private void RemoveBTN_Click(object sender, EventArgs e) {
            if (RylMessageBox.ShowDialog("Are your sure you want to delete this user?", "Delete User",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) {
                Account.DeleteUser(int.Parse(UsersGRD.SelectedRows[0].Cells[0].Value.ToString()));
                LoadUsers();
            }
        }

        private void AddBTN_Click(object sender, EventArgs e) {
            UsernameBX.Clear();
            CurrentBX.Clear();
            NewBX.Clear();
            CurrentBX.Enabled = false;
            CurrentPassPic.Visible = false;
            UsersGRDPNL.Visible = false;
            EditUserPNL.Visible = true;
            CloseBTN.Visible = false;
            AdminRDBTN.Enabled = true;
            ClerkRDBTN.Enabled = true;
            AboutLBL.Visible = false;
            SaveBTN.Text = "ADD";
        }

        private void CurrentPassPic_MouseDown(object sender, MouseEventArgs e) {
            CurrentBX.PasswordChar = '\0';
        }

        private void NewPassPic_MouseDown(object sender, MouseEventArgs e) {
            NewBX.PasswordChar = '\0';
        }

        private void CurrentPassPic_MouseUp(object sender, MouseEventArgs e) {
            CurrentBX.PasswordChar = '●';
        }

        private void NewPassPic_MouseUp(object sender, MouseEventArgs e) {
            NewBX.PasswordChar = '●';
        }
    }
}