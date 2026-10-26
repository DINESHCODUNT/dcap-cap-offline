using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace DigitalCap.Core.Enumerations
{
    public enum ShipType
    {
        // [Description("Bulk Carrier")]
        BulkCarrier = 1,
        // [Description("Bulk Liquid Carrier (other than oil or chemical)")]
        BulkLiquidCarrier = 2,
        // [Description("Chemical Carrier")]
        ChemicalCarrier = 3,
        // [Description("Gas Carrier")]
        GasCarrier = 4,
        // [Description("General Cargo Carrier")]
        GeneralCargoCarrier = 5,
        //  [Description("Offshore Supply Vessel")]
        OffshoreSupplyVessel = 6,
        // [Description("Offshore Support Vessel")]
        OffshoreSupportVessel = 7,
        // [Description("Oil Carrier")]
        OilCarrier = 8,
        //  [Description("Oil or Bulk/Ore (OBO) Carrier")]
        OilorBulk = 9,
        // [Description("Ore or Oil Carrier")]
        OreorOilCarrier = 10,
        //  [Description("Self Elevating Unit")]
        SelfElevatingUnit = 11,
        //  [Description("Tank Barge")]
        TankBarge = 12,
        //  [Description("Tug")]
        Tug = 13,
        //  [Description("Passenger Vessel")]
        PassengerVessel = 14
    }
}
