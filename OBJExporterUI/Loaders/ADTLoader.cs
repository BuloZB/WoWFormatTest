﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WoWFormatLib.FileReaders;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using static OBJExporterUI.Renderer.Structs;

namespace OBJExporterUI.Loaders
{
    class ADTLoader
    {
        public static Terrain LoadADT(string filename, CacheStorage cache, int shaderProgram)
        {
            WoWFormatLib.Structs.ADT.ADT adt = new WoWFormatLib.Structs.ADT.ADT();

            Terrain result = new Terrain();

            //Load ADT from file
            if (WoWFormatLib.Utils.CASC.cascHandler.FileExists(filename))
            {
                var adtreader = new ADTReader();
                adtreader.LoadADT(filename);
                adt = adtreader.adtfile;
            }
            else
            {
                throw new Exception("ADT " + filename + " does not exist!");
            }

            float TileSize = 1600.0f / 3.0f; //533.333
            float ChunkSize = TileSize / 16.0f; //33.333
            float UnitSize = ChunkSize / 8.0f; //4.166666
            float MapMidPoint = 32.0f / ChunkSize;

            List<Vertex> verticelist = new List<Vertex>();
            List<Int32> indicelist = new List<Int32>();

            result.vao = GL.GenVertexArray();
            GL.BindVertexArray(result.vao);

            result.vertexBuffer = GL.GenBuffer();
            result.indiceBuffer = GL.GenBuffer();

            List<Material> materials = new List<Material>();

            for (int ti = 0; ti < adt.textures.filenames.Count(); ti++)
            {
                Material material = new Material();
                material.filename = adt.textures.filenames[ti];
                material.textureID = BLPLoader.LoadTexture(adt.textures.filenames[ti], cache);
                materials.Add(material);
            }

            var initialChunkY = adt.chunks[0].header.position.Y;
            var initialChunkX = adt.chunks[0].header.position.X;

            List<RenderBatch> renderBatches = new List<RenderBatch>();

            for (uint c = 0; c < adt.chunks.Count(); c++)
            {
                var chunk = adt.chunks[c];

                int off = verticelist.Count();

                RenderBatch batch = new RenderBatch();

                for (int i = 0, idx = 0; i < 17; i++)
                {
                    for (int j = 0; j < (((i % 2) != 0) ? 8 : 9); j++)
                    {
                        Vertex v = new Vertex();
                        v.Normal = new Vector3(chunk.normals.normal_0[idx], chunk.normals.normal_1[idx], chunk.normals.normal_2[idx]);
                        if (chunk.vertexShading.red != null && chunk.vertexShading.red[idx] != 127)
                        {
                            v.Color = new Vector3(chunk.vertexShading.blue[idx] / 255.0f, chunk.vertexShading.green[idx] / 255.0f, chunk.vertexShading.red[idx] / 255.0f);
                        }
                        else
                        {
                            v.Color = new Vector3(1.0f, 1.0f, 1.0f);
                        }

                        // Commented out for maptexture hack
                        v.TexCoord = new Vector2(((float)j + (((i % 2) != 0) ? 0.5f : 0f)) / 8f, ((float)i * 0.5f) / 8f);

                        v.Position = new Vector3(chunk.header.position.X - (i * UnitSize * 0.5f), chunk.header.position.Y - (j * UnitSize), chunk.vertices.vertices[idx++] + chunk.header.position.Z);

                        if ((i % 2) != 0) v.Position.Y -= 0.5f * UnitSize;

                        verticelist.Add(v);
                    }
                }

                result.startPos = verticelist[0];

                batch.firstFace = (uint)indicelist.Count();
                for (int j = 9; j < 145; j++)
                {
                    indicelist.AddRange(new Int32[] { off + j + 8, off + j - 9, off + j });
                    indicelist.AddRange(new Int32[] { off + j - 9, off + j - 8, off + j });
                    indicelist.AddRange(new Int32[] { off + j - 8, off + j + 9, off + j });
                    indicelist.AddRange(new Int32[] { off + j + 9, off + j + 8, off + j });
                    if ((j + 1) % (9 + 8) == 0) j += 9;
                }
                batch.numFaces = (uint)(indicelist.Count()) - batch.firstFace;

                var layermats = new List<uint>();
                var alphalayermats = new List<int>();
                for (int li = 0; li < adt.texChunks[c].layers.Count(); li++)
                {
                    if(adt.texChunks[c].alphaLayer != null){
                        alphalayermats.Add(BLPLoader.GenerateAlphaTexture(adt.texChunks[c].alphaLayer[li].layer));
                    }
                    layermats.Add((uint)cache.materials[adt.textures.filenames[adt.texChunks[c].layers[li].textureId].ToLower()]);
                }
                batch.materialID = layermats.ToArray();
                batch.alphaMaterialID = alphalayermats.ToArray();

                int[] indices = indicelist.ToArray();
                Vertex[] vertices = verticelist.ToArray();

                Console.WriteLine("Vertices in array: " + vertices.Count());
                Console.WriteLine("Indices in array: " + indices.Count());

                GL.BindBuffer(BufferTarget.ArrayBuffer, result.vertexBuffer);
                GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(vertices.Count() * 11 * sizeof(float)), vertices, BufferUsageHint.StaticDraw);

                //var normalAttrib = GL.GetAttribLocation(shaderProgram, "normal");
                //GL.EnableVertexAttribArray(normalAttrib);
                //GL.VertexAttribPointer(normalAttrib, 3, VertexAttribPointerType.Float, false, sizeof(float) * 11, sizeof(float) * 0);

                //var colorAttrib = GL.GetAttribLocation(shaderProgram, "color");
                //GL.EnableVertexAttribArray(colorAttrib);
                //GL.VertexAttribPointer(colorAttrib, 3, VertexAttribPointerType.Float, false, sizeof(float) * 11, sizeof(float) * 3);

                var texCoordAttrib = GL.GetAttribLocation(shaderProgram, "texCoord");
                GL.EnableVertexAttribArray(texCoordAttrib);
                GL.VertexAttribPointer(texCoordAttrib, 2, VertexAttribPointerType.Float, false, sizeof(float) * 11, sizeof(float) * 6);

                var posAttrib = GL.GetAttribLocation(shaderProgram, "position");
                GL.EnableVertexAttribArray(posAttrib);
                GL.VertexAttribPointer(posAttrib, 3, VertexAttribPointerType.Float, false, sizeof(float) * 11, sizeof(float) * 8);

                GL.BindBuffer(BufferTarget.ElementArrayBuffer, result.indiceBuffer);
                GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(indices.Length * sizeof(int)), indices, BufferUsageHint.StaticDraw);

                renderBatches.Add(batch);
            }

            result.renderBatches = renderBatches.ToArray();
            //result.doodads = doodads.ToArray();
            //result.worldModelBatches = worldModelBatches.ToArray();

            cache.terrain.Add(filename, result);
            return result;
        }
    }
}