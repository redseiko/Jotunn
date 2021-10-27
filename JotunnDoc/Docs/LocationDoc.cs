﻿using System;
using System.IO;
using System.Text;
using Jotunn.Managers;
using UnityEngine;

namespace JotunnDoc.Docs
{
    class LocationDoc : Doc
    {

        public LocationDoc() : base("locations/location-list.md")
        {
            ZoneManager.OnVanillaLocationsAvailable += docLocations;
        }

        private void docLocations()
        {
            if (Generated)
            {
                return;
            }

            Jotunn.Logger.LogInfo("Documenting locations");

            AddHeader(1, "Location list");
            AddText("All of the locations currently in the game.");
            AddText("This file is automatically generated from Valheim using the JotunnDoc mod found on our GitHub.");
            AddTableHeader("Preview", "Biome", "BiomeArea", "Name", "Properties", "Quantity", "Exterior radius", "Altitude", "Distance", "Terrain delta");

            var imageDirectory = Path.Combine(BepInEx.Paths.PluginPath, nameof(JotunnDoc), "Docs", $"images/locations");
            Directory.CreateDirectory(imageDirectory);

            int latch = 0;

            foreach (var zoneLocation in ZoneSystem.instance.m_locations)
            {
                if (!zoneLocation.m_enable || !zoneLocation.m_prefab)
                {
                    continue;
                }

                var path = Path.Combine(imageDirectory, $"{zoneLocation.m_prefab.name}.png");
                var renderRequest = new RenderManager.RenderRequest(zoneLocation.m_prefab)
                {
                    Rotation = Quaternion.Euler(-24, -231, 26),
                    FieldOfView = 20f,
                    DistanceMultiplier = 1.1f
                };
                latch++;
                RenderManager.Instance.EnqueueRender(renderRequest, (Sprite sprite) =>
                {
                    if (sprite)
                    {
                        var texture = sprite.texture;
                        var bytes = texture.EncodeToPNG();
                        File.WriteAllBytes(path, bytes);
                    }

                    AddTableRow(
                        sprite ? $"![{zoneLocation.m_prefab.name}](../../images/locations/{zoneLocation.m_prefab.name}.png)" : "",
                        ToString(zoneLocation.m_biome),
                        ToString(zoneLocation.m_biomeArea),
                        zoneLocation.m_prefab.name,
                        GetProperties(zoneLocation),
                        zoneLocation.m_quantity.ToString(),
                        zoneLocation.m_exteriorRadius.ToString(), 
                        $"{zoneLocation.m_minAltitude} - {zoneLocation.m_maxAltitude}",
                        $"{zoneLocation.m_minDistance} - {zoneLocation.m_maxDistance}",
                        $"{zoneLocation.m_minTerrainDelta} - {zoneLocation.m_maxTerrainDelta}"
                    );
                    if (latch-- <= 0)
                    {
                        Save();
                    }
                });
            }

        }

        private string GetProperties(ZoneSystem.ZoneLocation zoneLocation)
        {
            return $"<ul>" +
                $"{(zoneLocation.m_prioritized ? "<li>Prioritized</li>" : "")}" +
                $"{(zoneLocation.m_unique ? "<li>Unique</li>" : "")}" +
                $"{(zoneLocation.m_group == "" ? "" : $"<li>Group: </li>{zoneLocation.m_group}</li>")}" +
                $"{(zoneLocation.m_snapToWater ? "<li>Snap to water</li>" : "")}" +
                $"{(zoneLocation.m_centerFirst ? "<li>Place in center first</li>":"")}" +
                $"</ul>";
        }

        private string ToString(Heightmap.BiomeArea biomeArea)
        {
            StringBuilder biomeAreas = new StringBuilder("<ul>");
            foreach (Heightmap.BiomeArea area in Enum.GetValues(typeof(Heightmap.BiomeArea)))
            {
                if (area == Heightmap.BiomeArea.Everything || (biomeArea & area) == 0)
                {
                    continue;
                }
                biomeAreas.Append($"<li>{area}</li>");
            }

            biomeAreas.Append("</ul>");

            return biomeAreas.ToString();
        }

        private string ToString(Heightmap.Biome biome)
        {
            StringBuilder biomeAreas = new StringBuilder("<ul>");

            foreach (Heightmap.Biome area in Enum.GetValues(typeof(Heightmap.Biome)))
            {
                if (area == Heightmap.Biome.BiomesMax || (biome & area) == 0)
                {
                    continue;
                }

                biomeAreas.Append($"<li>{area}</li>");
            }

            biomeAreas.Append("</ul>");

            return biomeAreas.ToString();
        }
    }
}
