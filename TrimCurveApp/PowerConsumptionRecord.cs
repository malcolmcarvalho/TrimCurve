namespace TrimCurveApp {
    class PowerConsumptionRecord {
        public double Draft { get; private set; }
        public double Speed { get; private set; }
        public double Trim { get; private set; }
        public double Power { get; private set; }
        public double PowerSavings { get; private set; }

        public PowerConsumptionRecord(double draft, double speed, double trim, double power, double percentage) {
            Draft = draft;
            Speed = speed;
            Trim = trim;
            Power = power;
            PowerSavings = percentage;
        }
    }
}
