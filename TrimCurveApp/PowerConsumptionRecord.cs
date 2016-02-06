using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrimCurveApp
{
    class PowerConsumptionRecord
    {
        public double Draft { get; private set; }
        public double Speed { get; private set; }
        public double Trim { get; private set; }
        public double Power { get; private set; }
        public double PowerSavingPercentage { get; private set; }

        public PowerConsumptionRecord(double draft, double speed, double trim, double power, double percentage)
        {
            Draft = draft;
            Speed = speed;
            Trim = trim;
            Power = power;
            PowerSavingPercentage = percentage;
        }
    }
}
