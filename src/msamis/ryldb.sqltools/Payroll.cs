﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Org.BouncyCastle.Asn1.X509;


namespace MSAMISUserInterface {
    /// <summary>
    /// Do not serialize objects when payroll is pending.
    /// </summary>
    public class Payroll {
        #region Adjustement Operationen

        public void GetAdjustments() { }

        #endregion


        #region Fields Definition

        #region Meta

        public int GID;
        public HourProcessor TotalHours = new HourProcessor();
        public Attendance.Period period = Attendance.GetCurrentPayPeriod();
        private double _BasicPayHourly;
        private Dictionary<string, double> _rates = new Dictionary<string, double>();

        public Dictionary<string, HourCostPair> TotalSummary = new Dictionary<string, HourCostPair> {
            #region + Keys Definition

            {"normal_nsu", new HourCostPair()},
            {"normal_sun", new HourCostPair()},
            {"regular_nsu", new HourCostPair()},
            {"regular_sun", new HourCostPair()},
            {"special_nsu", new HourCostPair()},
            {"special_sun", new HourCostPair()},
            {"special", new HourCostPair()},
            {"normal", new HourCostPair()},
            {"regular", new HourCostPair()},
            {"total", new HourCostPair()}

            #endregion
        };

        public Dictionary<string, HourCostPair> hc = new Dictionary<string, HourCostPair> {
            #region + Keys Definition

            {"nsu_proper_day_normal", new HourCostPair()},
            {"nsu_proper_day_special", new HourCostPair()},
            {"nsu_proper_day_regular", new HourCostPair()},
            {"nsu_proper_night_normal", new HourCostPair()},
            {"nsu_proper_night_special", new HourCostPair()},
            {"nsu_proper_night_regular", new HourCostPair()},
            {"nsu_overtime_day_normal", new HourCostPair()},
            {"nsu_overtime_day_special", new HourCostPair()},
            {"nsu_overtime_day_regular", new HourCostPair()},
            {"nsu_overtime_night_normal", new HourCostPair()},
            {"nsu_overtime_night_special", new HourCostPair()},
            {"nsu_overtime_night_regular", new HourCostPair()},
            {"sun_proper_day_normal", new HourCostPair()},
            {"sun_proper_day_special", new HourCostPair()},
            {"sun_proper_day_regular", new HourCostPair()},
            {"sun_proper_night_normal", new HourCostPair()},
            {"sun_proper_night_special", new HourCostPair()},
            {"sun_proper_night_regular", new HourCostPair()},
            {"sun_overtime_day_normal", new HourCostPair()},
            {"sun_overtime_day_special", new HourCostPair()},
            {"sun_overtime_day_regular", new HourCostPair()},
            {"sun_overtime_night_normal", new HourCostPair()},
            {"sun_overtime_night_special", new HourCostPair()},
            {"sun_overtime_night_regular", new HourCostPair()},

            #endregion
        };

        public static Hours total_old;

        private int _PayrollId;

        #endregion

        #region Derivables
        public double Bonuses => ThirteenthMonthPay + Cola + EmergencyAllowance + CashBond;
        public double Deductions => Sss + PagIbig + PhilHealth + CashAdvance + Withtax;
        public double GrossPay => TotalSummary["total"].total + Bonuses;
        public double NetPay => GrossPay - Deductions;
        public double TaxableIncome => GrossPay - Sss - PagIbig - PhilHealth;
        public double Excess => GrossPay - TaxableIncome;

        #endregion

        #region Bonuses

        public double EmergencyAllowance {
            get { return _emergencyallowance; }
            set {
                _emergencyallowance = value;
                if (_viewmode==0) {
                    string query = $@"UPDATE `msadb`.`payroll` SET `emergencyallowance`='{value}' WHERE `PID`='{_PayrollId}';";
                    SQLTools.ExecuteNonQuery(query); 
                }
            }
        }

        public double CashBond {
            get { return _cashbond; }
            set {
                _cashbond = value;
                if (_viewmode == 0) {
                    string query = $@"UPDATE `msadb`.`payroll` SET `cashbond`='{value}' WHERE `PID`='{_PayrollId}';";
                    SQLTools.ExecuteNonQuery(query); 
                }
            }
        }

        public double Cola {
            get { return _cola; }
            set {
                _cola = value;
                if (_viewmode == 0) {
                    string query = $@"UPDATE `msadb`.`payroll` SET `cola`='{value}' WHERE `PID`='{_PayrollId}';";
                    SQLTools.ExecuteNonQuery(query); 
                }
            }
        }

        public double ThirteenthMonthPay {
            get { return _thirteen; }
            set {
                _thirteen = value;
                if (_viewmode == 0) {
                    string query = $@"UPDATE `msadb`.`payroll` SET `thirteenth`='{value}' WHERE `PID`='{_PayrollId}';";
                    SQLTools.ExecuteNonQuery(query); 
                }
            }
        }

        #endregion
        #region Deductions

        public double CashAdvance {
            get { return _cashadv; }
            set {
                _cashadv = value;
                string query = $@"UPDATE `msadb`.`payroll` SET `cashadv`='{value}' WHERE `PID`='{_PayrollId}';";
                SQLTools.ExecuteNonQuery(query);
            }
        }

        public double PagIbig;
        public double PhilHealth;
        public double Sss;
        public double Withtax;
        public WithTax wt = new WithTax();

        #endregion

        // PayrollData Containers

        #region Internals

        private double _cashbond;
        private double _cola;
        private double _emergencyallowance;

        private double _thirteen;
        private double _cashadv;

        #endregion

        #endregion Fields Definition
        
        #region Constructors

        private DataRow DbValues;
        /// <summary>
        /// Creates a payroll object for a specified guard during a specific
        /// date. Use this for manual creation / unapproved payrolls.
        /// </summary>
        /// <param name="GID">Guard ID</param>
        /// <param name="month">Month of payroll</param>
        /// <param name="period">Period of payroll [1/2]</param>
        /// <param name="year">Year of payroll</param>
        public Payroll(int GID, int month, int period, int year) {
            sw.Start();
            this.GID = GID;
            this.period.period = period;
            this.period.month = month;
            this.period.year = year;
            // Create DataTable for future calls.
            DataTable d =
                SQLTools.ExecuteQuery(
                    $@"select count(*) as `c` from payroll where gid={GID} AND month={month} AND period={period} AND year={year}");
            this._BasicPayHourly = _GetMyBasicPays() / 8.00;
            // If record is not present yet (while object is created).
            if (d.Rows[0][0].ToString() == "0") {
                string insertionquery = $@"
                    INSERT INTO `msadb`.`payroll`
                    (`GID`, `month`, `period`, `year`, `cashbond`, `pagibig`, `philhealth`, `cola`, `emergencyallowance`, `pstatus`)
                    VALUES ('{this.GID}', '{this.period.month}', '{this.period.period}', '{this.period.year}', 
                    '{RetrieveDefaultCashBond()}', '{RetriveDefaultPagibig()}', '{RetrieveDefaultPhilhealth()}', 
                    '{RetrieveDefaultCola()}', '{RetrieveDefaultEmergency()}', '{Enumeration.PayrollStatus.Pending}');";
                SQLTools.ExecuteNonQuery(insertionquery);
            }

            DbValues =
                SQLTools.ExecuteQuery(
                    $@"select * from payroll where gid={GID} AND month={month} AND period={period} AND year={year}").Rows[0];
            this._PayrollId = int.Parse(DbValues["PID"].ToString());
            if (DbValues["pstatus"].ToString() == Enumeration.PayrollStatus.Approved.ToString()) {
                _InitMeApproved();
            }
            else  _InitMeSoftly(month, year);
            

            
        }
        private Stopwatch sw = new Stopwatch();
        public int _viewmode;
        private void _InitMeApproved() {
            this._viewmode = 1;
            this.GID = int.Parse(DbValues["GID"].ToString());
            // Set the derivables
            this.EmergencyAllowance = double.Parse(DbValues["emergencyallowance"].ToString());
            this.CashBond = double.Parse(DbValues["cashbond"].ToString());
            this.Cola = double.Parse(DbValues["cola"].ToString());
            this.ThirteenthMonthPay = double.Parse(DbValues["thirteenth"].ToString());
            this.CashAdvance = double.Parse(DbValues["cashadv"].ToString());
            // Set the primitives
            this.PagIbig = double.Parse(DbValues["pagibig"].ToString());
            this.PhilHealth = double.Parse(DbValues["philhealth"].ToString());
            this.Sss = double.Parse(DbValues["sss"].ToString());
            this.Withtax =  double.Parse(DbValues["withtax"].ToString());
            this.hc = (Dictionary<string, HourCostPair>) _DeserializeObject(
                System.Text.Encoding.Default.GetString((byte[])DbValues["hc_serializable"])
                );
            this.TotalSummary = (Dictionary<string, HourCostPair>)_DeserializeObject(
                System.Text.Encoding.Default.GetString((byte[])DbValues["totalsummary_serializable"])
                );
            sw.Stop();
            Console.WriteLine("Done\nObject Deserialization Method: {0}", sw.Elapsed.TotalSeconds);
        }

        private void _InitMeSoftly(int month, int year) {
            this._viewmode = 0;
            this._InitRates();
            ComputeHours();
            Compute();
            sw.Stop();
            Console.WriteLine("Done\nManual Calculation Method: {0}", sw.Elapsed.TotalSeconds);
        }

        #endregion

        #region Methods

        public void Compute() {
            Compute(true);
        }

        public void Approve() {
            // Create serialized dictionary objects
            // Why? There are too many fields in this dictionary.
            string serialized_totalsummary = SerializeTotalSummary();
            string serialized_hc = SerializeHc();

            // Finally, insert these values to the database, saved the 'Always Updating' fields 
            // (Those with auto-properties)
            string q = $@" 
                UPDATE `msadb`.`payroll` SET `rates_id`='{this.rates_id}', `withtax`='{this.Withtax}', `sss`={this.Sss},
                `totalsummary_serializable`='{serialized_totalsummary}', `hc_serializable`='{serialized_hc}',
                `pstatus`={Enumeration.PayrollStatus.Approved}, `basicpayhourly`={_BasicPayHourly}
                WHERE `PID`='{_PayrollId}';";
            SQLTools.ExecuteQuery(q);

        }

        

        #endregion

        #region  Computationes

        private double _GetMyBasicPays() {
            return double.Parse(SQLTools.ExecuteSingleResult("select amount from basicpay where status=1"));
        }
        private double _GetMyBasicPays(int BPID) {
            return double.Parse(SQLTools.ExecuteSingleResult($@"select amount from basicpay where bpid={BPID}"));
        }

        public void Compute(bool checkthirteen) {
            foreach (var key in hc.Keys.ToList())
                hc[key] = new HourCostPair(TotalHours.GetHourDictionary()[key].TotalHours, _BasicPayHourly * _rates[key]);
            ComputeTotalSummary();
            ComputeBonuses(checkthirteen);
            ComputeDeductions();
            //MessageBox.Show(SQLTools.SerializeMe(hc));
        }

        public void ComputeHours() {
            var HourIterationDataTable = SQLTools.ExecuteQuery(
                string.Format(@"
                    select dutydetails.did, atid, date, timein, timeout, 
                    ti_hh, ti_mm, ti_period, to_hh, to_mm, to_period
                    from attendance 
                    left join period on period.pid = attendance.pid
                    left join dutydetails on attendance.did = dutydetails.did
                    left join sduty_assignment on sduty_assignment.aid = dutydetails.aid
                    left join guards on guards.gid=sduty_assignment.gid 
                    where period.period = " + period.period + " and month=" + period.month + " and year=" +
                              period.year + @" and guards.gid=" + GID + @"
            ;", GID, period.period, period.year, period.month));
            var HourIterationTotal = new Hours();
            foreach (DataRow dr in HourIterationDataTable.Rows) {
                var ti = dr["date"].ToString().Substring(0, 11) + dr["TimeIn"];
                var to = dr["date"].ToString().Substring(0, 10) + " " + dr["TimeOut"];
                var TimeInDateTime = DateTime.Parse(ti);
                var TimeOutDateTime = DateTime.Parse(to);

                var StartDutyString = dr["date"].ToString().Substring(0, 11) + " " + dr["ti_hh"] + ":" + dr["ti_mm"] +
                                      " " + dr["ti_period"];
                var EndDutyString = dr["date"].ToString().Substring(0, 11) + " " + dr["to_hh"] + ":" + dr["to_mm"] +
                                    " " + dr["to_period"];
                var DutyStart = DateTime.Parse(StartDutyString);
                var DutyEnd = DateTime.Parse(EndDutyString);
                var hp = new HourProcessor(TimeInDateTime, TimeOutDateTime, DutyStart, DutyEnd);
                TotalHours.Add(hp);
            }
        }

        private void ComputeTotalSummary() {
            TotalSummary["normal_nsu"] =
                hc["nsu_proper_day_normal"] +
                hc["nsu_overtime_day_normal"] +
                hc["nsu_proper_night_normal"] +
                hc["nsu_overtime_night_normal"];
            TotalSummary["normal_sun"] =
                hc["sun_proper_day_normal"] +
                hc["sun_overtime_day_normal"] +
                hc["sun_proper_night_normal"] +
                hc["sun_overtime_night_normal"];
            TotalSummary["regular_nsu"] =
                hc["nsu_proper_day_regular"] +
                hc["nsu_overtime_day_regular"] +
                hc["nsu_proper_night_regular"] +
                hc["nsu_overtime_night_regular"];
            TotalSummary["regular_sun"] =
                hc["sun_proper_day_regular"] +
                hc["sun_overtime_day_regular"] +
                hc["sun_proper_night_regular"] +
                hc["sun_overtime_night_regular"];
            TotalSummary["special_sun"] =
                hc["sun_proper_day_special"] +
                hc["sun_overtime_day_special"] +
                hc["sun_proper_night_special"] +
                hc["sun_overtime_night_special"];
            TotalSummary["special_nsu"] =
                hc["nsu_proper_day_special"] +
                hc["nsu_overtime_day_special"] +
                hc["nsu_proper_night_special"] +
                hc["nsu_overtime_night_special"];
            TotalSummary["normal"] =
                hc["nsu_proper_day_normal"] +
                hc["nsu_overtime_day_normal"] +
                hc["nsu_proper_night_normal"] +
                hc["nsu_overtime_night_normal"] +
                hc["sun_proper_day_normal"] +
                hc["sun_overtime_day_normal"] +
                hc["sun_proper_night_normal"] +
                hc["sun_overtime_night_normal"];
            TotalSummary["regular"] =
                hc["nsu_proper_day_regular"] +
                hc["nsu_overtime_day_regular"] +
                hc["nsu_proper_night_regular"] +
                hc["nsu_overtime_night_regular"] +
                hc["sun_proper_day_regular"] +
                hc["sun_overtime_day_regular"] +
                hc["sun_proper_night_regular"] +
                hc["sun_overtime_night_regular"];
            TotalSummary["special"] =
                hc["nsu_proper_day_special"] +
                hc["nsu_overtime_day_special"] +
                hc["nsu_proper_night_special"] +
                hc["nsu_overtime_night_special"] +
                hc["sun_proper_day_special"] +
                hc["sun_overtime_day_special"] +
                hc["sun_proper_night_special"] +
                hc["sun_overtime_night_special"];
            TotalSummary["total"] =
                TotalSummary["special"] + TotalSummary["regular"] + TotalSummary["normal"];
        }

        public double ComputeNet() {
            var e = GrossPay;
            e -= Deductions;
            e += Bonuses;
            return e;
        }

        

        public void ComputeDeductions() {
            Sss = ComputeSSS(GetSssContribId(new DateTime(period.year, period.month, period.period == 1 ? 1 : 16)));
            if (period.period == 2) {
                PagIbig = RetriveDefaultPagibig();
                PhilHealth = RetrieveDefaultPhilhealth();
            }
            else {
                PagIbig = 0;
                PhilHealth = 0;
            }

            CashAdvance = RetrieveCashAdvance();
            
            
            ComputeWithTax(TaxableIncome, Excess);
        }

        #region Retrievables

        public static double RetrieveCashAdvance() {
            string w = Data.PayrollIni["Payroll"]["DefaultCashAdvance"];
            return double.Parse(w);
        }

        public static double RetrieveDefaultPhilhealth() {
            string w = Data.PayrollIni["Payroll"]["DefaultPHIC"];
            return double.Parse(w);
        }

        public static double RetriveDefaultPagibig() {
            string w = Data.PayrollIni["Payroll"]["DefaultHDMF"];
            return double.Parse(w);
        }

        public static double RetrieveDefaultCashBond() {
            string w = Data.PayrollIni["Payroll"]["DefaultCashBond"];
            return double.Parse(w);
        }

        public static double RetrieveDefaultCola() {
            string w = Data.PayrollIni["Payroll"]["DefaultCola"];
            return double.Parse(w);
        }

        public static double RetrieveDefaultEmergency() {
            string w = Data.PayrollIni["Payroll"]["DefaultEmer"];
            return double.Parse(w);
        }

        #endregion
        #region Computables

        public void ComputeBonuses(bool ca) {
            if (ca)
                if (period.month == 12 && period.period == 2) ThirteenthMonthPay = ComputeThirteen();
                else ThirteenthMonthPay = 0;
            _cola = double.Parse(DbValues["cola"].ToString());
            _emergencyallowance = double.Parse(DbValues["emergencyallowance"].ToString());
            _cashbond = double.Parse(DbValues["cashbond"].ToString());
        }

        public void ComputeBonuses() {
            ComputeBonuses(true);
        }

        public void ComputeWithTax(double Taxable, double Excess) {
            var NumDependents = Guard.GetNumberOfDependents(GID);
            if (NumDependents > 4)
                NumDependents = 4;
            var sm = "s" + NumDependents + "me" + NumDependents;
            var w = new WithTax();
            // Start Paper code
            var prevbracket = 0;
            double prevtaxbracket = 0;
            var dt = SQLTools.ExecuteQuery("Select * from withtax_bracket where estatus='" + sm + "'");
            foreach (DataRow dr in dt.Rows)
                if (double.Parse(dr["bracket"].ToString()) > Taxable &&
                    Taxable > prevtaxbracket) {
                    var dt2 = SQLTools.ExecuteQuery("select * from withtax_value where wid=" +
                                                    (int.Parse(dr["taxid"].ToString()) - 1));

                    w.TaxbaseD = double.Parse(dt2.Rows[0]["value"].ToString());
                    w.excessfactor = int.Parse(dt2.Rows[0]["excessmult"].ToString());
                    break;
                }
                else {
                    prevtaxbracket = double.Parse(dr["bracket"].ToString());
                    prevbracket++;
                }
            wt.TaxbaseD = w.TaxbaseD;
            wt.excessfactor = w.excessfactor;

            var ___ = Excess * ((double) w.excessfactor / 100);
            wt.total = w.TaxbaseD + Excess * ((double) w.excessfactor / 100);
            wt.ExcessTax = Excess * ((double) w.excessfactor / 100);

            Withtax =  w.TaxbaseD + Excess * ((double) w.excessfactor / 100);
        }

        public WithTax GetWithholdingTax() {
            return wt;
        }

        public double ComputeSSS(int contrib_id) {
            var ssscontrib = SQLTools.ExecuteQuery($"select * from ssscontrib where contrib_id='{contrib_id}'");
            foreach (DataRow dr in ssscontrib.Rows)
                if (double.Parse(dr["range_start"].ToString()) < GrossPay &&
                    GrossPay < double.Parse(dr["range_end"].ToString())) {
                    return double.Parse(dr["ec"].ToString());
                    break;
                }
            return 50.00;
        }

        public double ComputeThirteen() {
            {
                // TODO: Run this code only on every January 1-4
                var py = new List<Payroll>();
                for (var mm = 1; mm <= 12; mm++) {
                    py.Add(new Payroll(GID, mm, 1, period.year));
                    py.Add(new Payroll(GID, mm, 2, period.year));
                }
                var hce = new HourProcessor();
                double qwe = 0;
                foreach (var pypy in py) pypy.ComputeHours();
                foreach (var pypy in py) qwe += pypy.TotalHours.GetTotalTS().TotalHours * _BasicPayHourly;
                return qwe / 12.00;
            }
        }

        #endregion

        #endregion

        #region Basic Pay Operations

        public static string GetCurrentBasicPay() {
            return SQLTools.ExecuteSingleResult("select amount from basicpay where status = 1");
        }


        /// <summary>
        ///     Gets the basic pay during a certain date.
        ///     Use for historical payroll viewing.
        /// </summary>
        /// <param name="dt">Date to search</param>
        /// <returns></returns>
        public static double GetBasicPay(DateTime dt) {
            var q =
                $@"
select bpid, amount,start,end, case status when 1 then 'Active' when 2 then 'Pending' when 0 then 'Inactive' end as status from basicpay order by start desc
";
            var d = SQLTools.ExecuteQuery(q);
            foreach (DataRow dr in d.Rows) {
                var dstart = DateTime.ParseExact(dr["start"].ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var dend =
                    !dr["end"].ToString().Equals("-1")
                        ? DateTime.ParseExact(dr["end"].ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : new DateTime(9999, 12, 28);
                if (dstart < dt && dt < dend) {
                    return double.Parse(dr["amount"].ToString());
                    break;
                }
            }
            return 0.00;
        }

        /// <summary>
        ///     Overrides the current basic pay. Sets previous basic pay to Inactive if Date Effective
        ///     has already elapsed.
        ///     <param name="start">Date Effective (can be in the future)</param>
        ///     <param name="pay"></param>
        /// </summary>
        public static void AddBasicPay(DateTime start, double pay) {
            var ss = start.ToString("yyyy-MM-dd");
            var spay = pay.ToString("#.##");
            var HasElapsed = DateTime.Now >= start;
            if (HasElapsed) {
                var q2 = $"select bpid from basicpay where status=1";
                var bpid = SQLTools.GetInt(q2);
                q2 = $"UPDATE `msadb`.`basicpay` SET `status`='0', `end`='{ss}' WHERE `BPID`='{bpid}'";
                SQLTools.ExecuteNonQuery(q2);
            }
            var q =
                $"INSERT INTO `msadb`.`basicpay` (`amount`, `start`, `end`, `status`) VALUES ('{spay}', '{ss}', '{-1}', '{(HasElapsed ? Enumeration.BasicPayStatus.Active : Enumeration.BasicPayStatus.Future)}');";
            SQLTools.ExecuteNonQuery(q);
        }

        /// <summary>
        ///     Adds a basic pay for some historic point.
        ///     This method simply inserts a
        ///     basic pay value with start and end dates.
        ///     This method does not override
        ///     the current basic pay, and its status is set to
        ///     default 0 (inactive).
        ///     Does not check for overlaps.
        /// </summary>
        /// <param name="start">Date Effective</param>
        /// <param name="end">Date Terminated</param>
        /// <param name="pay">Basic Pay Value (per shift)</param>
        public static void AddBasicPay(DateTime start, DateTime end, double pay) {
            var ss = start.ToString("yyyy-MM-dd");
            var es = end.ToString("yyyy-MM-dd");
            var q =
                $"INSERT INTO `msadb`.`basicpay` (`amount`, `start`, `end`, `status`) VALUES ('{pay:#.##}', '{ss}', '{es}', '0');";
            SQLTools.ExecuteNonQuery(q);
        }


        public static DataTable GetBasicPayHistory() {
            var q = @"use msadb;
select bpid, amount,start,case `end`
when '-1' then 'Pending' 
when end then end
end as 'end', case status when 1 then 'Active' when 2 then 'Pending' when 0 then 'Inactive' end as status from basicpay order by start desc";
            return SQLTools.ExecuteQuery(q);
        }

        #endregion

        #region Accessor Functions Operations

        #region + SSS Contribution CRUD Methods

        #region SSS: For DataTable CRUD

        public static DataTable GetSssContribTable() {
            return SQLTools.ExecuteQuery($@"select sssid, range_start, range_end, ec from ssscontrib
            right join contribdetails
            on ssscontrib.contrib_id=contribdetails.contrib_id
            where status={Enumeration.ContribStatus.Active}");
        }

        public static DataTable GetSssContribTable(int contrib_id) {
            return SQLTools.ExecuteQuery($@"select sssid, range_start, range_end, ec from ssscontrib
            right join contribdetails
            on contribdetails.contrib_id = ssscontrib.contrib_id
            where ssscontrib.contrib_id='{contrib_id}';");
        }

        public static DataTable GetSssContribList() {
            return SQLTools.ExecuteQuery(
                $@"select * from contribdetails where type='{
                        Enumeration.ContribType.Sss
                    }' order by date_effective desc");
        }


        #region SSS:old

        private static void EditSSSContrib(int SssId, double RangeStart, double RangeEnd, double Value) {
            var q =
                @"UPDATE `msadb`.`ssscontrib` SET `range_start`='{0}', `range_end`='{1}', `ec`='{2}' WHERE `sssid`='" +
                SssId + "';";
            q = string.Format(q, RangeStart, RangeEnd, Value);
            SQLTools.ExecuteNonQuery(q);
        }

        private static void RemoveSSSContrib(int SssId) {
            SQLTools.ExecuteNonQuery("delete from ssscontrib WHERE `sssid`='" + SssId + "';");
        }


        private static void AddSSSContrib(double RangeStart, double RangeEnd, double Value) {
            var q = string.Format(
                @"INSERT INTO `msadb`.`ssscontrib` (`range_start`, `range_end`, `ec`) VALUES ('{0}', '{1}','{2}');",
                RangeStart, RangeEnd, Value);
            SQLTools.ExecuteNonQuery(q);
        }

        #endregion

        #endregion For DataTable CRUD

        #region SSS: For Computation CRUD

        /*
         * TODO: Now na naa na ko'y Get Contrib IDs, replace generic sss function with `where contrib_id=x` gg ez 😂😂😂
         */
        public static int GetSssContribId(DateTime date) {
            return _GetContribId(date, Enumeration.ContribType.Sss.ToString());
        }

        public static int GetWithTaxContribId(DateTime date) {
            return _GetContribId(date, Enumeration.ContribType.WithTax.ToString());
        }

        /// <summary>
        ///     Gets the `contrib_id` of contribution of a specific date.
        ///     Use this Id for further queries and calculations.
        /// </summary>
        /// <param name="date">Date</param>
        /// <returns>Contrib Id.</returns>
        private static int _GetContribId(DateTime date, string val) {
            var q = $"select * from contribdetails where type={val}";
            var contrib_id = 0;
            var dt = SQLTools.ExecuteQuery(q);
            foreach (DataRow dr in dt.Rows) {
                var ss = DateTime.Parse(dr["date_effective"].ToString());
                var es = dr["date_dissolved"].ToString().Equals("-1")
                    ? SQLTools.GetDateTime("9999-12-28")
                    : DateTime.Parse(dr["date_dissolved"].ToString());
                if (ss < date && date < es) {
                    contrib_id = int.Parse(dr["contrib_id"].ToString());
                    break;
                }
            }
            return contrib_id;
        }

        #endregion

        #region SSS: DB Saving

        public static void SaveSssContrib(DataGridView dt, DateTime date_effective) {
            var date_effectives = date_effective.ToString("yyyy-MM-dd");
            var HasElapsed = DateTime.Now >= date_effective;

            // Create ContribDetails Table (main)

            // if date has already elapsed (adding historical data)
            if (HasElapsed) {
                var q2 = $@"
UPDATE `msadb`.`contribdetails` SET 
`date_dissolved`='{date_effectives}'
WHERE type ={Enumeration.ContribType.Sss} AND status={Enumeration.ContribStatus.Active}";
                SQLTools.ExecuteNonQuery(q2);
            }

            var q =
                $@"INSERT INTO `msadb`.`contribdetails` (`date_effective`, `date_dissolved`, `type`, `status`) VALUES ('{
                        date_effectives
                    }', '{-1}', '{Enumeration.ContribType.Sss}', '{
                        (HasElapsed ? Enumeration.ContribStatus.Past : Enumeration.ContribStatus.Future)
                    }');";
            SQLTools.ExecuteNonQuery(q);
            var contrib_id = SQLTools.GetInt("select LAST_INSERT_ID();");


            // Create Actual SSS connections
            foreach (DataGridViewRow dr in dt.Rows) {
                var from = _cleanstringmoney(dr.Cells[1].Value.ToString());
                var to = _cleanstringmoney(dr.Cells[3].Value.ToString());
                var value = _cleanstringmoney(dr.Cells[5].Value.ToString());
                var w =
                    $"INSERT INTO `msadb`.`ssscontrib` (`range_start`, `range_end`, `ec`, `contrib_id`) VALUES ('{from}', '{to}', '{value}', '{contrib_id}');";
                SQLTools.ExecuteNonQuery(w);
            }
        }

        private static string _cleanstringmoney(string s) {
            return Regex.Replace(s, @"[^0-9\-\.]", "");
        }

        #endregion

        #endregion


        #region + GetGuardsList Methods Min/Max

        public static DataTable GetGuardsPayrollMain(string search, int status) {
            string q;
            if (status == -1) {
                q = $@"     select guards.gid, concat(ln,', ',fn,' ',mn) as name, client.name, (
                                                        CASE 
                                                            WHEN (period.pid IS NOT NULL AND dutydetails.did IS NOT NULL)
                                                            THEN 'Yes'
                                                            ELSE 'No Attendance Details Found'
                                                        END
                                                      ) AS attendance, 

                case pstatus
            when {Enumeration.PayrollStatus.Approved} then 'Approved'
            when {Enumeration.PayrollStatus.Calculated} then 'Calculated'
            when {Enumeration.PayrollStatus.Pending} then 'Pending'
            end as pstatus

 
                                                from request
                                                left join client on client.cid=request.cid
                                                left join request_assign on request_assign.rid=request.rid
                                                left join sduty_assignment on sduty_assignment.raid=request_assign.raid
                                                left join guards on guards.gid=sduty_assignment.gid
                                                left join period on guards.gid=period.gid
												left join dutydetails on dutydetails.aid=sduty_assignment.aid
												left join payroll on guards.gid=payroll.gid
                                                where RequestType = 1 " + search + " group by guards.gid ";
            }
            else {
                q = @"     select guards.gid, concat(ln,', ',fn,' ',mn) as name, client.name, (
                                                        CASE 
                                                            WHEN (period.pid IS NOT NULL AND dutydetails.did IS NOT NULL)
                                                            THEN 'Yes'
                                                            ELSE 'No Attendance Details Found'
                                                        END
                                                      ) AS attendance, pstatus
                                                from request
                                                left join client on client.cid=request.cid
                                                left join request_assign on request_assign.rid=request.rid
                                                left join sduty_assignment on sduty_assignment.raid=request_assign.raid
                                                left join guards on guards.gid=sduty_assignment.gid
                                                left join period on guards.gid=period.gid
												left join dutydetails on dutydetails.aid=sduty_assignment.aid
												left join payroll on guards.gid=payroll.gid
                                                where RequestType = 1 " + search + " AND pstatus = '" + status +
                    "' group by guards.gid ";


            }
            return SQLTools.ExecuteQuery(q);
        }

        public static DataTable GetGuardsPayrollMinimal() {
            return SQLTools.ExecuteQuery(@"     select guards.gid, concat(ln,', ',fn,' ',mn) as name
                                                from request
                                                left join client on client.cid=request.cid
                                                left join request_assign on request_assign.rid=request.rid
                                                left join sduty_assignment on sduty_assignment.raid=request_assign.raid
                                                left join guards on guards.gid=sduty_assignment.gid
                                                left join period on guards.gid=period.gid
                                                where RequestType = 1 
                                                
                                                group by guards.gid
                                                ");
        }

        #endregion


        #region DB Operations

        #region WithTax: DB Ops

        public static DataTable GetWithTaxHeaders(int contrib_id) {
            return SQLTools.ExecuteQuery(
                $@"SELECT wid, value, excessmult, contribdetails.contrib_id FROM msadb.withtax_value 
right join withtax_bracket on withtax_bracket.taxid=withtax_value.wid
left join contribdetails on contribdetails.contrib_id=withtax_bracket.contrib_id where contribdetails.contrib_id='{
                        contrib_id
                    }' group by wid;");
        }

        public static DataTable GetWithTaxBrackets(int contrib_id) {
            var q = $@"select * from withtax_bracket where contrib_id={contrib_id};";
            return SQLTools.ExecuteQuery(q);
        }

        public static DataTable GetWithTaxContribList() {
            return SQLTools.ExecuteQuery($@"    select contrib_id, date_effective, date_dissolved, case status 
                                                when 1 then 'Active'
                                                when 2 then 'Pending'
                                                when 0 then 'Inactive'
                                                end as `status`
                                                from contribdetails 
                                                where type='{
                    Enumeration.ContribType.WithTax
                }' order by date_effective desc");
        }

        #endregion

        #endregion DB Operations


        public HourProcessor GetTotalHours() {
            return TotalHours;
        }



        #endregion For DataTable CRUD

        #region Sub-classes

        public class WithTax {
            public int excessfactor;
            public double ExcessTax;
            public double TaxbaseD;
            public double total;
        }

        #endregion

        #region This.Rates initializers
        private void _InitRates() {
            int id = 0;
            string w = $"select rates_id, date_effective, date_dissolved from rates";
            DataTable dt = SQLTools.ExecuteQuery(w);
            foreach (DataRow dr in dt.Rows) {
                DateTime s = DateTime.Parse(dr["date_effective"].ToString());
                DateTime e = dr["date_dissolved"].ToString().Equals("-1") ? new DateTime(9999,12,28) : DateTime.Parse(dr["date_dissolved"].ToString());
                DateTime me = new DateTime(period.year, period.month,
                    (period.period == 1 ? 1 : 16), 0,0,10);
                if (s < me && me < e) {
                    id = int.Parse(dr["rates_id"].ToString());
                    this.rates_id = id;
                    break;
                }
            }
            _InitRates(id);
        }

        private void _InitRates(int id) {
            string q =
                #region + string q = {long query}
                $@"
            select
            ordinary_day as nsu_proper_day_normal,
            special_holiday as nsu_proper_day_special,
            regular_holiday as nsu_proper_day_regular,
            (ordinary_day * nightdifferential) as nsu_proper_night_normal,
            (regular_holiday * nightdifferential) as nsu_proper_night_regular,
            (ordinary_day * overtime) as nsu_overtime_day_normal,
            (special_holiday * overtime_holiday) as nsu_overtime_day_special,
            (regular_holiday * overtime_holiday) as nsu_overtime_day_regular,
            (ordinary_day * overtime * nightdifferential) as nsu_overtime_night_normal,
            (special_holiday * overtime_holiday * nightdifferential) as nsu_overtime_night_special,
            (regular_holiday * overtime_holiday * nightdifferential) as nsu_overtime_night_regular,
            sunday_ordinary_day as sun_proper_day_normal,
            sunday_special_holiday as sun_proper_day_special,
            sunday_regular_holiday as sun_proper_day_regular,
            (sunday_ordinary_day * nightdifferential) as sun_proper_night_normal,
            (sunday_special_holiday * nightdifferential) as sun_proper_night_special,
            (sunday_regular_holiday * nightdifferential) as sun_proper_night_regular,
            (sunday_ordinary_day * overtime) as sun_overtime_day_normal,
            (sunday_special_holiday * overtime_holiday) as sun_overtime_day_special,
            (sunday_regular_holiday * overtime_holiday) as sun_overtime_day_regular,
            (sunday_ordinary_day * overtime * nightdifferential) as sun_overtime_night_normal,
            (sunday_special_holiday * overtime_holiday * nightdifferential) as sun_overtime_night_special,
            (sunday_regular_holiday * overtime_holiday * nightdifferential) as sun_overtime_night_regular,
            (ordinary_day * nightdifferential) as nsu_proper_night_special
            from rates where rates_id = {id}
            ";
            #endregion
            DataTable dt2 = SQLTools.ExecuteQuery(q);
            foreach (string key in _rateKeys) {
                this._rates.Add(key, double.Parse(dt2.Rows[0][key].ToString()));
            }
        }
        private int rates_id;

        #endregion
        public static DateTime GetNextPayday() {
            var p = DateTime.Now;
            if (5 <= p.Day && p.Day < 19)
                return new DateTime(p.Year, p.Month, 20);
            else if ((1 <= p.Day && p.Day < 5)) return new DateTime(p.Year, p.Month, 5);
            else if (20 <= p.Day && p.Day <= 31) {
                p = p.AddMonths(1);
                return new DateTime(p.Year, p.Month, 5);
            }
            return new DateTime(0, 0, 0);
        }
        #region Defaults Setter
        private static readonly string[] _rateKeys = new string[] {
            #region +keys definition
            "nsu_proper_day_normal", "nsu_proper_day_special", "nsu_proper_day_regular", "nsu_proper_night_normal",
            "nsu_proper_night_special", "nsu_proper_night_regular", "nsu_overtime_day_normal",
            "nsu_overtime_day_special", "nsu_overtime_day_regular", "nsu_overtime_night_normal",
            "nsu_overtime_night_special", "nsu_overtime_night_regular", "sun_proper_day_normal",
            "sun_proper_day_special", "sun_proper_day_regular", "sun_proper_night_normal", "sun_proper_night_special",
            "sun_proper_night_regular", "sun_overtime_day_normal", "sun_overtime_day_special",
            "sun_overtime_day_regular", "sun_overtime_night_normal", "sun_overtime_night_special",
            "sun_overtime_night_regular"
            #endregion
        };
        

        public static DataTable GetRatesList() {
            return SQLTools.ExecuteQuery("SELECT * FROM msadb.rates;");
        }


        public static void SetRates(DateTime date_effective, double special_holiday, double regular_holiday,
            double sunday_ordinary_day, double sunday_special_holiday, double sunday_regular_holiday,
            double nightdifferential, double overtime, double overtime_holiday) {
            var ss = date_effective.ToString("yyyy-MM-dd");
            var HasElapsed = DateTime.Now >= date_effective;
            if (HasElapsed) {
                var q2 = $"select rates_id from rates where status=1";
                var rates_id = SQLTools.GetInt(q2);
                q2 = $"UPDATE `msadb`.`rates` SET `status`='0', `end`='{ss}' WHERE `BPID`='{rates_id}'";
                SQLTools.ExecuteNonQuery(q2);
            }
            var kwiri =
                $@"INSERT INTO `msadb`.`rates` 
                (`date_effective`, `date_dissolved`, `ordinary_day`, `special_holiday`, 
                `regular_holiday`, `sunday_ordinary_day`, `sunday_special_holiday`, `sunday_regular_holiday`,
                `nightdifferential`, `overtime`, `overtime_holiday`, `status`) 
                VALUES ('{ss}', '{-1}', '{1.00}', '{special_holiday.ToString("N2")}', 
                '{regular_holiday.ToString("N2")}', '{sunday_ordinary_day.ToString("N2")}', '{sunday_special_holiday.ToString("N2")}', '{sunday_regular_holiday.ToString("N2")}', 
                '{nightdifferential.ToString("N2")}', '{overtime.ToString("N2")}', '{overtime_holiday.ToString("N2")}', '{
                        (HasElapsed ? Enumeration.BasicPayStatus.Active : Enumeration.BasicPayStatus.Future)
                    }');";
            SQLTools.ExecuteNonQuery(kwiri);
        }

        public static DataTable GetRates(int rates_id) {
            return SQLTools.ExecuteQuery($@"
SELECT rates_id, date_effective, date_dissolved, contrib_id, ordinary_day, special_holiday, regular_holiday, sunday_ordinary_day, sunday_special_holiday, sunday_regular_holiday, nightdifferential, overtime, overtime_holiday,
case status 
when {Enumeration.ContribStatus.Active} then 'Active'
when {Enumeration.ContribStatus.Future} then 'Pending'
when {Enumeration.ContribStatus.Past} then 'Inactive'
end as status
 FROM msadb.rates
where rates_id={rates_id};
");
        }

        public static void SetBonusDefaults(double philhealth, double pagibig, double cashbond, double cola,
            double emergencyallowance) {
            //Data.PayrollIni["Payroll"]["DefaultCashAdvance"] = cashadv.ToString(CultureInfo.InvariantCulture);
            Data.PayrollIni["Payroll"]["DefaultPHIC"] = philhealth.ToString("N2");
            Data.PayrollIni["Payroll"]["DefaultHDMF"] = pagibig.ToString("N2");
            Data.PayrollIni["Payroll"]["DefaultCashBond"] = cashbond.ToString("N2"); ;
            Data.PayrollIni["Payroll"]["DefaultCola"] = cola.ToString("N2");
            Data.PayrollIni["Payroll"]["DefaultEmer"] = emergencyallowance.ToString("N2");
            Data.iniparser.WriteFile(Data.PayrollIniLocation,Data.PayrollIni);
            Data.InitPayrollConfig();
        }



        public string[] GetSSSDetails() {
            return new string[] {
                "December 27, 2014 - January 10, 1970", "3340.00 - 5500.00", "281"
            };
        }




        #endregion


       



        private string SerializeHc() {
            using (MemoryStream stream = new MemoryStream()) {
                new BinaryFormatter().Serialize(stream, this.hc);
                return Convert.ToBase64String(stream.ToArray());
            }
        }
        private string SerializeTotalSummary() {
            using (MemoryStream stream = new MemoryStream()) {
                new BinaryFormatter().Serialize(stream, this.TotalSummary);
                return Convert.ToBase64String(stream.ToArray());
            }
        }
        private static object _DeserializeObject(string str) {
            byte[] bytes = Convert.FromBase64String(str);
            using (MemoryStream stream = new MemoryStream(bytes)) {
                return new BinaryFormatter().Deserialize(stream);
            }
        }



       

        public DataTable GetAdjustmentHistory() {
            var w = $@"
            SELECT case AdjType 
            when {Enumeration.AdjTypes.CashAdvance} then 'Cash Adv.'
            when {Enumeration.AdjTypes.CashBond} then 'Cash Bond'
            when {Enumeration.AdjTypes.Cola} then 'Cola'
            when {Enumeration.AdjTypes.EmergencyAllowance} then 'Emer. Allowance'
            when {Enumeration.AdjTypes.Thirteenth} then '13th Month'
            end as adjtype,
            adjdate as addedon,
            value 
            FROM msadb.adjustment_log where PID='{this._PayrollId}'";
            return SQLTools.ExecuteQuery(w);
        }

    }
}