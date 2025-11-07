// *********************************************************************** Assembly : PathFinder
// Author : xenonsmurf Created : 04-03-2020 Created : 04-03-2020 Created : 04-03-2020 Created :
// Created : 04-03-2020 Created : 04-03-2020 Created : 04-03-2020 Created :
//
// Last Modified By : xenonsmurf Last Modified On : 04-04-2020 Last Modified On : 04-12-2020 Last
// Modified On : 07-04-2020 ***********************************************************************
// <copyright file="FFXINAVTOOLS.cs" company="Xenonsmurf">
//     Copyright © 2020
// </copyright>
// <summary>
// </summary>
// ***********************************************************************
using System;
using System.Collections.Generic;
using System.Threading;

namespace PathFinder.Common
{
    /// <summary>
    /// Class FFXINAV.
    /// </summary>
    public partial class FFXINAV
    {
        /// <summary>
        /// Gets or sets the waypoints.
        /// </summary>
        /// <value>The waypoints.</value>
        public List<position_t> Waypoints { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FFXINAV"/> class.
        /// </summary>
        public FFXINAV()
        {
            Waypoints = new List<position_t>();
        }

        /// <summary>
        /// Determines whether this instance [can we see destination] the specified start.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        /// <returns>
        /// <c>true</c> if this instance [can we see destination] the specified start; otherwise, <c>false</c>.
        /// </returns>
        public bool CanWeSeeDestination(position_t start, position_t end)
        {
            return CanSeeDestination(start, end);
        }

        /// <summary>
        /// Unloads this instance.
        /// </summary>
        public void Unload()
        {
            unload();
        }

        /// <summary>
        /// Initializes the specified pathsize.
        /// </summary>
        /// <param name="pathsize">The pathsize.</param>
        public void Initialize(int pathsize)
        {
            initialize(pathsize);
        }

        /// <summary>
        /// Loads the specified file.
        /// </summary>
        /// <param name="file">The file.</param>
        public void Load(string file)
        {
            load(file);
        }

        /// <summary>
        /// Loads the ob jfile.
        /// </summary>
        /// <param name="file">The file.</param>
        public void LoadOBJfile(string file)
        {
            Initialize(100);
            Thread.Sleep(2000);
            LoadOBJFile(file);
        }

        /// <summary>
        /// Gets or sets a value indicating whether [dumping mesh].
        /// </summary>
        /// <value><c>true</c> if [dumping mesh]; otherwise, <c>false</c>.</value>
        public bool DumpingMesh { get; set; } = false;

        /// <summary>
        /// Dumps the nav mesh.
        /// </summary>
        /// <param name="file">The file.</param>
        public void Dump_NavMesh(string file)
        {
            if (DumpingMesh == false)
            {
                DumpingMesh = true;
                LoadOBJFile(file);
                Thread.Sleep(5000);
                DumpNavMesh(file);
                DumpingMesh = false;
            }
        }

        /// <summary>
        /// Gets the error message.
        /// </summary>
        /// <returns>System.String.</returns>
        public string GetErrorMessage()
        {
            return getLogMessage().ToString();
        }

        /// <summary>
        /// Finds the path to posi.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        /// <param name="UseCustonNavMeshes">if set to <c>true</c> [use custon nav meshes].</param>
        public void FindPathToPosi(position_t start, position_t end, bool UseCustonNavMeshes)
        {
            //set false if using DSP Nav files
            //set true if using Meshes made with Noesis map data
            findPath(start, end, UseCustonNavMeshes);
        }

        /// <summary>
        /// Determines whether [is nav mesh enabled].
        /// </summary>
        /// <returns><c>true</c> if [is nav mesh enabled]; otherwise, <c>false</c>.</returns>
        public bool IsNavMeshEnabled()
        {
            return isNavMeshEnabled();
        }

        /// <summary>
        /// Pathes the count.
        /// </summary>
        /// <returns>System.Int32.</returns>
        public int PathCount()
        {
            return pathpoints();
        }

        /// <summary>
        /// Gets the waypoints.
        /// </summary>
        public unsafe void GetWaypoints()
        {
            Waypoints.Clear();
            if (pathpoints() > 0)
            {
                double* xitems;
                double* zitems;
                int itemsCount;

                using (FFXINAV.Get_WayPoints_Wrapper(out xitems, out zitems, out itemsCount))
                {
                    for (int i = 0; i < itemsCount; i++)
                    {
                        var position = new position_t { X = (float)xitems[i], Z = (float)zitems[i] };
                        Waypoints.Add(position);
                    }
                }
            }
        }

        /// <summary>
        /// Changes the nav mesh settings.
        /// </summary>
        /// <param name="CellSize">Size of the cell.</param>
        /// <param name="CellHeight">Height of the cell.</param>
        /// <param name="AgentHeight">Height of the agent.</param>
        /// <param name="AgentRadius">The agent radius.</param>
        /// <param name="MaxClimb">The maximum climb.</param>
        /// <param name="MaxSlope">The maximum slope.</param>
        /// <param name="TileSize">Size of the tile.</param>
        /// <param name="RegionMinSize">Minimum size of the region.</param>
        /// <param name="RegionMergeSize">Size of the region merge.</param>
        /// <param name="EdgeMaxLen">Maximum length of the edge.</param>
        /// <param name="EdgeError">The edge error.</param>
        /// <param name="VertsPP">The verts pp.</param>
        /// <param name="DetailSampDistance">The detail samp distance.</param>
        /// <param name="DetailMaxError">The detail maximum error.</param>
        public void ChangeNavMeshSettings(double CellSize, double CellHeight, double AgentHeight, double AgentRadius, double MaxClimb,
         double MaxSlope, double TileSize, double RegionMinSize, double RegionMergeSize, double EdgeMaxLen, double EdgeError, double VertsPP,
         double DetailSampDistance, double DetailMaxError)
        {
            navMeshSettings(CellSize, CellHeight, AgentHeight, AgentRadius, MaxClimb, MaxSlope, TileSize,
                RegionMinSize, RegionMergeSize, EdgeMaxLen, EdgeError, VertsPP, DetailSampDistance, DetailMaxError);
        }

        /// <summary>
        /// Converts to single.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>System.Single.</returns>
        public static float ToSingle(double value)
        {
            return (float)value;
        }

        /// <summary>
        /// Distances to wall.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <returns>System.Double.</returns>
        public double DistanceToWall(position_t start)
        {
            try
            {
                if (start.X != 0 && start.Z != 0)
                {
                    return GetDistanceToWall(start);
                }
                else
                    return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return 0;
            }
        }

        /// <summary>
        /// Getrotations the specified start.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        /// <returns>System.SByte.</returns>
        public sbyte Getrotation(position_t start, position_t end)
        {
            return GetRotation(start, end);
        }
    }
}