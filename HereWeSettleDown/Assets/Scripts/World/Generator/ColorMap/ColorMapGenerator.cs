﻿using System.Collections.Generic;
using UnityEngine;
using World.Mesh;
using World.Generator.Biomes;

namespace World.Generator.ColorMap
{
    [CustomGenerator(8, true, "modHeightMap", "biomes", "biomeMasks", "globalBiomeMask")]
    public class ColorMapGenerator : SubGenerator
    {
        public DisplayType displayType;
        [Range(0, 3)] 
        public int smoothIterations;

        public override void OnGenerate()
        {
            GenerateColorPackMap(GenerateSimpleColorMap());
            GenerationCompleted();
        }

        public Color[] GenerateSimpleColorMap()
        {
            float[,] heightMap = GetValue<float[,]>("modHeightMap");
            Biome[] biomes = GetValue<Biome[]>("biomes");

            BiomeMask[] biomeMasks = GetValue<BiomeMask[]>("biomeMasks");
            int[,] globalBiomeMask = GetValue<int[,]>("globalBiomeMask");

            Color[] colorMap = null;

            switch (displayType)
            {
                case (DisplayType.GameView):
                    colorMap = ColorMapFromColorRegions(heightMap, biomeMasks);
                    break;

                case (DisplayType.Falloff):
                    if (TryGetValue("falloffMap", out float[,] falloffMap))
                        colorMap = ColorMapFromHeightMap(falloffMap);
                    break;

                case (DisplayType.Biomes):
                    colorMap = ColorMapFromBiomes(globalBiomeMask, biomes);
                    break;
            }

            if (colorMap == null)
                colorMap = ColorMapFromHeightMap(heightMap);

            return colorMap;
        }

        public void GenerateColorPackMap(Color[] colorMap)
        {
            int width = GetValue<int>("mapWidth");
            int height = GetValue<int>("mapHeight");

            ColorPack[,] convertedColorMap = ConvertColorMap(width, height, colorMap);
            for (int i = 0; i < smoothIterations; i++)
                convertedColorMap = SmoothColorMap(convertedColorMap);

            values["colorMap"] = convertedColorMap;
        }

        public ColorPack[,] ConvertColorMap(int width, int height, Color[] _colors)
        {
            ColorPack[,] colorMap = EmptyColorMap(width - 1, height - 1);

            for (int x = 0; x < width; x ++)
            {
                for (int y = 0; y < height; y ++)
                {
                    Color targetColor = _colors[width * y + x];

                    // Color main quad
                    if (x < width - 1 && y < height - 1)
                        colorMap[x, y][0] = targetColor;

                    // Color left-bottom quad
                    if (x - 1 >= 0 && y - 1 > 0)
                        colorMap[x - 1, y - 1][1] = targetColor;

                    // Color left quad
                    if (x - 1 >= 0 && y < height - 1)
                        colorMap[x - 1, y] = new ColorPack(targetColor, targetColor);

                    // Color bottom quad
                    if (y - 1 >= 0 && x < width - 1)
                        colorMap[x, y - 1] = new ColorPack(targetColor, targetColor);
                }
            }

            return colorMap;
        }

        public ColorPack[,] SmoothColorMap(ColorPack[,] colorMap)
        {
            int width = colorMap.GetLength(0);
            int height = colorMap.GetLength(1);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    ColorPack colorPack = colorMap[x, y];
                    for (int i = 0; i < 2; i ++)
                    {
                        int sign = i == 0 ? 1 : -1;
                        List<Color> neighborColors = new List<Color>();

                        // First triangle
                        neighborColors.Add(colorPack[i + sign]);
                        if (x - sign >= 0 && x - sign < width)
                            neighborColors.Add(colorMap[x - sign, y][i + sign]);
                        if (y - sign >= 0 && y - sign < height)
                            neighborColors.Add(colorMap[x, y - sign][i + sign]);

                        if (TwoSameColors(neighborColors.ToArray(), out Color newColor))
                            colorPack[i] = newColor;
                    }
                    colorMap[x, y] = colorPack;
                }
            }
            return colorMap;
        }

        private bool TwoSameColors(Color[] colors, out Color targetColor)
        {
            for (int i = 0; i < colors.Length; i ++)
            {
                for (int j = i + 1; j < colors.Length; j ++)
                {
                    if (colors[i] == colors[j])
                    {
                        targetColor = colors[i];
                        return true;
                    }
                }
            }
            targetColor = Color.black;
            return false;
        }

        public ColorPack[,] EmptyColorMap(int width, int height)
        {
            ColorPack[,] colorMap = new ColorPack[width , height];

            // Create empty colorPack map
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    colorMap[x, y] = new ColorPack();
                }
            }

            return colorMap;
        }

        public Color[] ColorMapFromHeightMap(float[,] heightMap)
        {
            int width = heightMap.GetLength(0);
            int height = heightMap.GetLength(1);

            Color[] colorMap = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    colorMap[y * width + x] = Color.Lerp(Color.black, Color.white, heightMap[x, y]);
                }
            }

            return colorMap;
        }

        public Color[] ColorMapFromColorRegions(float[,] heightMap, BiomeMask[] biomeMasks)
        {
            // Create color map
            int width = heightMap.GetLength(0);
            int height = heightMap.GetLength(1);

            Color[] colorMap = new Color[width * height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int i = 0; i < biomeMasks.Length; i++)
                    {
                        if (biomeMasks[i].mask[x, y] == 1)
                        {
                            if (biomeMasks[i].biome.overrideColors || colorMap[y * width + x] == default)
                            {
                                foreach (BiomeColorRegion colorRegion in biomeMasks[i].biome.colorRegions)
                                {
                                    if (heightMap[x, y] <= colorRegion.height)
                                    {
                                        colorMap[y * width + x] = colorRegion.color;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return colorMap;
        }

        public Color[] ColorMapFromBiomes(int[,] globalBiomeMask, Biome[] biomes)
        {
            int width = globalBiomeMask.GetLength(0);
            int height = globalBiomeMask.GetLength(1);

            // Set dicitionary with index to color
            Dictionary<int, Color> indToColor = new Dictionary<int, Color>();
            for (int i = 0; i < biomes.Length; i++)
                if (!indToColor.ContainsKey(biomes[i].index))
                    indToColor.Add(biomes[i].index, biomes[i].mapColor);

            // Create color map
            Color[] colorMap = new Color[width * height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (indToColor.ContainsKey(globalBiomeMask[x, y]))
                        colorMap[y * width + x] = indToColor[globalBiomeMask[x, y]];
                }
            }

            return colorMap;
        }

        public enum DisplayType
        {
            Noise,
            Biomes,
            Falloff,
            GameView
        }
    }
}