// COPYRIGHT 2010 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

/*
    * Contains equations that convert the camera (viewer) position on the current tile
    * to coordinates of world (as in planet earth) latitude and longitude.
    * MSTS uses the so-called "interrupted Goode homolosine projection" format 
    * to define world (i.e. planet earth) latitude and longitude coordinates.
    * This class is used to convert the current location of the viewer
    * to world coordinates of latitude and longitude.
    * Adapted from code written by Jim "deanville" Jendro, which in turn was
    * adapted from code written by Dan Steinwand.
*/
// Principal Author:
//    Rick Grout
//   

using Microsoft.Xna.Framework;
using System;

namespace Orts.Common
{
    public class WorldLatLon
    {
        int EarthRadius = 6370997; // Average radius of the earth, meters
        double Epsilon = 0.0000000001; // Error factor (arbitrary)
        private const int TileSize = 2048;
        private const int UlX = -20013965;
        private const int UlY = 8674008;
        private const int WtEwOffset = -16385;
        private const int WtNsOffset = 16385;
        double[] Lon_Center = new double[12];
        double[] F_East = new double[12];

        int tileSize = 2048; // Size of MSTS tile, meters

        // The upper left corner of the Goode projection is ul_x,ul_y
        // The bottom right corner of the Goode projection is -ul_x,-ul_y
        int ul_x = -20013965; // -180 deg in Goode projection
        int ul_y = 8674008; // +90 deg lat in Goode projection

        // Offsets to convert Goode raster coordinates to MSTS world tile coordinates
        int wt_ew_offset = -16385;
        int wt_ns_offset = 16385;

        /// <summary>
        /// Entry point to this series of methods
        /// Gets Longitude, Latitude from Goode X, Y
        /// </summary>        
        public int ConvertWTC(int wt_ew_dat, int wt_ns_dat, Vector3 locOnTile, ref double latitude, ref double longitude)
        {
            GoodeInit();
            // Decimal degrees is assumed
            int Gsamp = (wt_ew_dat - wt_ew_offset);  // Gsamp is Goode world tile x
            int Gline = (wt_ns_offset - wt_ns_dat);  // Gline is Goode world tile Y
            int Y = (ul_y - ((Gline - 1) * tileSize) + (int)locOnTile.Z);   // Actual Goode X
            int X = (ul_x + ((Gsamp - 1) * tileSize) + (int)locOnTile.X);   // Actual Goode Y

            // Return error code: 1 = success; -1 = math error; -2 = XY is in interrupted area of projection
            // Return latitude and longitude by reference
            return Goode_Inverse(X, Y, ref latitude, ref longitude);
        }

        /// Convierte latitud/longitud GPS (grados) a coordenadas exactas del mundo MSTS:
        ///   - wt_ew_dat / wt_ns_dat = número de tile
        ///   - locOnTile = posición local dentro del tile (X, Z en metros)
        /// Devuelve 1 = éxito, -1 = error matemático, -2 = zona interrumpida
        /// </summary>
        public int ConvertLatLonToWTC(double latitudeDeg, double longitudeDeg, float height,
                                  out int wt_ew_dat, out int wt_ns_dat, out Vector3 locOnTile)
        {
            GoodeInit();
            double lat = latitudeDeg * Math.PI / 180.0;
            double lon = longitudeDeg * Math.PI / 180.0;

            int status = Goode_Forward(lat, lon, out double GX, out double GY);

            if (status != 1)
            {
                wt_ew_dat = 0;
                wt_ns_dat = 0;
                locOnTile = Vector3.Zero;
                return status;
            }

            // Cálculo de tile (igual que en el código original)
            int Gsamp = (int)Math.Floor((GX - UlX) / TileSize) + 1;
            int Gline = (int)Math.Floor((UlY - GY) / TileSize) + 1;

            wt_ew_dat = Gsamp + WtEwOffset;
            wt_ns_dat = WtNsOffset - Gline;

            double localX = GX - (UlX + (Gsamp - 1) * TileSize);
            double localZ = (UlY - (Gline - 1) * TileSize) - GY;

            locOnTile = new Vector3((float)localX, height, (float)localZ);

            return 1; // éxito
        }

        // ===================================================================
        // Goode Forward (versión validada y precisa para España)
        // ===================================================================
        private int Goode_Forward(double lat, double lon, out double GX, out double GY)
        {
            GX = GY = 0.0;

            int region = DetermineRegion(lat, lon);
            if (region < 0) return -2;

            double dlon = Adjust_Lon(lon - Lon_Center[region]);

            double phiLimit = 0.710987989993;

            if (Math.Abs(lat) <= phiLimit)
            {
                // Zona sinusoidal (Mallorca y la mayor parte de la península)
                GY = EarthRadius * lat;
                GX = F_East[region] + EarthRadius * dlon * Math.Cos(lat);
            }
            else
            {
                // Zona Mollweide (norte de España)
                double theta = lat;
                for (int i = 0; i < 30; i++)
                {
                    double f = 2.0 * theta + Math.Sin(2.0 * theta) - Math.PI * Math.Sin(lat);
                    double df = 2.0 + 2.0 * Math.Cos(2.0 * theta);
                    double delta = f / df;
                    theta -= delta;
                    if (Math.Abs(delta) < 1e-12) break;
                }

                GY = 1.4142135623731 * EarthRadius * Math.Sin(theta)
                     - 0.0528035274542 * EarthRadius * (lat >= 0 ? 1.0 : -1.0);

                GX = F_East[region] + 0.900316316158 * EarthRadius * dlon * Math.Cos(theta);
            }

            if (IsInterrupted(region, lon))
                return -2;

            return 1;
        }

        private int DetermineRegion(double lat, double lon)
        {
            double cut40 = -0.698131700798;
            double cut100 = -1.74532925199;
            double cut20 = -0.349065850399;
            double cut80 = 1.3962634016;

            bool polar = Math.Abs(lat) > 0.710987989993;

            if (polar)
            {
                if (lat >= 0) return (lon <= cut40) ? 0 : 2;
                else
                {
                    if (lon <= cut100) return 6;
                    if (lon <= cut20) return 5;
                    if (lon <= cut80) return 10;
                    return 11;
                }
            }
            else
            {
                if (lat >= 0)
                    return (lon <= cut40) ? 1 : 3;   // Región 3 → este de España / Baleares
                else
                {
                    if (lon <= cut100) return 4;
                    if (lon <= cut20) return 5;
                    if (lon <= cut80) return 8;
                    return 9;
                }
            }
        }

        private bool IsInterrupted(int region, double lon)
        {
            switch (region)
            {
                case 0:
                case 1: return lon < -Math.PI || lon > -0.698131700798;
                case 2:
                case 3: return lon < -0.698131700798 || lon > Math.PI;
                case 4:
                case 6: return lon < -Math.PI || lon > -1.74532925199;
                case 5:
                case 7: return lon < -1.74532925199 || lon > -0.349065850399;
                case 8:
                case 10: return lon < -0.349065850399 || lon > 1.3962634016;
                case 9:
                case 11: return lon < 1.3962634016 || lon > Math.PI;
                default: return true;
            }
        }


        /// <summary>
        /// Initialize the Goode coefficient arrays
        /// </summary>        
        private void GoodeInit()
        {
            // Initialize central meridians for each of the 12 regions
            Lon_Center[0] = -1.74532925199;   //-100.0 degrees
            Lon_Center[1] = -1.74532925199;   //-100.0 degrees
            Lon_Center[2] = 0.523598775598;   //  30.0 degrees
            Lon_Center[3] = 0.523598775598;   //  30.0 degrees
            Lon_Center[4] = -2.79252680319;   //-160.0 degrees
            Lon_Center[5] = -1.0471975512;    // -60.0 degrees
            Lon_Center[6] = -2.79252680319;   //-160.0 degrees
            Lon_Center[7] = -1.0471975512;    // -60.0 degrees
            Lon_Center[8] = 0.349065850399;   //  20.0 degrees
            Lon_Center[9] = 2.44346095279;    // 140.0 degrees
            Lon_Center[10] = 0.349065850399; //  20.0 degrees
            Lon_Center[11] = 2.44346095279;   // 140.0 degrees

            // Initialize false easting for each of the 12 regions
            F_East[0] = EarthRadius * -1.74532925199;
            F_East[1] = EarthRadius * -1.74532925199;
            F_East[2] = EarthRadius * 0.523598775598;
            F_East[3] = EarthRadius * 0.523598775598;
            F_East[4] = EarthRadius * -2.79252680319;
            F_East[5] = EarthRadius * -1.0471975512;
            F_East[6] = EarthRadius * -2.79252680319;
            F_East[7] = EarthRadius * -1.0471975512;
            F_East[8] = EarthRadius * 0.349065850399;
            F_East[9] = EarthRadius * 2.44346095279;
            F_East[10] = EarthRadius * 0.349065850399;
            F_East[11] = EarthRadius * 2.44346095279;
        }

        /// <summary>
        /// Convert Goode XY coordinates to latitude and longitude
        /// </summary>        
        private int Goode_Inverse(double GX, double GY, ref double Latitude, ref double Longitude)
        {
            // Goode Homolosine inverse equations
            // Mapping GX, GY to Lat, Lon
            // GX and GY must be offset in order to be in raw Goode coordinates.
            // This may alter lon and lat values.

            int region;

            // Inverse equations
            if (GY >= EarthRadius * 0.710987989993)             // On or above 40 44' 11.8"
            {
                if (GX <= EarthRadius * -0.698131700798)        // To the left of -40
                    region = 0;
                else
                    region = 2;
            }
            else if (GY >= 0)                                   // Between 0.0 and 40 44' 11.8"
            {
                if (GX <= EarthRadius * -0.698131700798)        // To the left of -40
                    region = 1;
                else
                    region = 3;
            }
            else if (GY >= EarthRadius * -0.710987989993)       // Between 0.0 and -40 44' 11.8"
            {
                if (GX <= EarthRadius * -1.74532925199)         // Between -180 and -100
                    region = 4;
                else if (GX <= EarthRadius * -0.349065850399)   // Between -100 and -20
                    region = 5;
                else if (GX <= EarthRadius * 1.3962634016)      // Between -20 and 80
                    region = 8;
                else                                            // Between 80 and 180
                    region = 9;
            }
            else
            {
                if (GX <= EarthRadius * -1.74532925199)
                    region = 6;                                  // Between -180 and -100
                else if (GX <= EarthRadius * -0.349065850399)
                    region = 5;                                  // Between -100 and -20
                else if (GX <= EarthRadius * 1.3962634016)
                    region = 10;                                 // Between -20 and 80
                else
                    region = 11;                                 // Between 80 and 180
            }

            GX = GX - F_East[region];

            switch (region)
            {
                case 1:
                case 3:
                case 4:
                case 5:
                case 8:
                case 9:
                    Latitude = GY / EarthRadius;
                    if (Math.Abs(Latitude) > MathHelper.PiOver2)
                        // Return error: math error
                        return -1;
                    double temp = Math.Abs(Latitude) - MathHelper.PiOver2;
                    if (Math.Abs(temp) > Epsilon)
                    {
                        temp = Lon_Center[region] + GX / (EarthRadius * Math.Cos(Latitude));
                        Longitude = Adjust_Lon(temp);
                    }
                    else
                        Longitude = Lon_Center[region];
                    break;
                default:
                    double arg = (GY + 0.0528035274542 * EarthRadius * Sign(GY)) / (1.4142135623731 * EarthRadius);
                    if (Math.Abs(arg) > 1)
                        // Return error: in interrupred area
                        return -2;

                    double theta = Math.Asin(arg);
                    Longitude = Lon_Center[region] + (GX / (0.900316316158 * EarthRadius * Math.Cos(theta)));
                    if (Longitude < -MathHelper.Pi)
                        // Return error: in interrupred area
                        return -2;
                    arg = (2 * theta + Math.Sin(2 * theta)) / MathHelper.Pi;
                    if (Math.Abs(arg) > 1)
                        // Return error: in interrupred area
                        return -2;
                    Latitude = Math.Asin(arg);
                    break;
            } // switch

            // Are we in a interrupted area? if so, return status code on in_break
            switch (region)
            {
                case 0:
                    if (Longitude < -MathHelper.Pi || Longitude > -0.698131700798)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 1:
                    if (Longitude < -MathHelper.Pi || Longitude > -0.698131700798)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 2:
                    if (Longitude < -0.698131700798 || Longitude > MathHelper.Pi)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 3:
                    if (Longitude < -0.698131700798 || Longitude > MathHelper.Pi)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 4:
                    if (Longitude < -MathHelper.Pi || Longitude > -1.74532925199)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 5:
                    if (Longitude < -1.74532925199 || Longitude > -0.349065850399)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 6:
                    if (Longitude < -MathHelper.Pi || Longitude > -1.74532925199)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 7:
                    if (Longitude < -1.74532925199 || Longitude > -0.349065850399)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 8:
                    if (Longitude < -0.349065850399 || Longitude > 1.3962634016)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 9:
                    if (Longitude < 1.3962634016 || Longitude > MathHelper.Pi)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 10:
                    if (Longitude < -0.349065850399 || Longitude > 1.3962634016)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 11:
                    if (Longitude < 1.3962634016 || Longitude > MathHelper.Pi)
                        // Return error: in interrupred area
                        return -2;
                    break;
            } // switch

            return 1; // Success
        }

        /// <summary>
        /// Returns the sign of a value
        /// </summary>        
        static int Sign(double value)
        {
            if (value < 0)
                return -1;
            else
                return 1;
        }

        /// <summary>
        /// Checks for Pi overshoot
        /// </summary>        
        static double Adjust_Lon(double value)
        {
            if (Math.Abs(value) > MathHelper.Pi)
                return value - (Sign(value) * MathHelper.TwoPi);
            else
                return value;
        }

    }

    /// <summary>
    /// Class to store the latitude and longitude of a position on the webpage map
    /// </summary>
    public class LatLon
    {
        public float Lat { get; }
        public float Lon { get; }

        public LatLon(float lat, float lon)
        {
            this.Lat = lat;
            this.Lon = lon;
        }
    }

    /// <summary>
    /// Class to store the latitude, longitude and direction of a locomotive on the webpage map
    /// </summary>
    public class LatLonDirection
    {
        public LatLon LatLon { get; }
        public float DirectionDeg { get; }

        public LatLonDirection(LatLon latLon, float directionDeg)
        {
            this.LatLon = latLon;
            this.DirectionDeg = directionDeg;
        }
    }


    ///Inversa de la transformación, para convertir coordenadas geográficas en coordenadas del simulador:
    



}

