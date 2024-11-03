using UnityEngine;

namespace Hollow.TerrainSystem
{
    internal class ProceduralMesh
    {
        private static Mesh cubeMesh;
        private static Mesh quadMesh;
        private static Mesh layerQuadMesh;
        
        public static Mesh HorizontalQuad
        {
            get
            {
                if (!quadMesh)
                {
                    CreateQuadMesh();
                }
                
                return quadMesh;
            }
        }
        
        /// <summary>
        /// Same as horizontal quad but lowered by 0.5 units and pivot is in the left bottom corner
        /// </summary>
        public static Mesh LayerHorizontalQuad
        {
            get
            {
                if (!layerQuadMesh)
                {
                    CreateLayerQuadMesh();
                }
                
                return layerQuadMesh;
            }
        }
        
        public static Mesh Cube
        {
            get
            {
                if (!cubeMesh)
                {
                    CreateCubeMesh();
                }
                
                return cubeMesh;
            }
        }
        
        private static void CreateLayerQuadMesh()
        {
            layerQuadMesh = new Mesh() { name = "Procedural HQuad Mesh" };

            layerQuadMesh.vertices = new[]
            {
                new Vector3(0f, -.5f, 0f),
                new Vector3(0f, -.5f, 1f),
                new Vector3(1f, -.5f, 0f),
                new Vector3(1f, -.5f, 1f),
            };

            layerQuadMesh.uv = new[]
            {
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 0),
                new Vector2(1, 1),
            };

            layerQuadMesh.triangles = new[]
            {
                0, 1, 2,
                2, 1, 3
            };

            layerQuadMesh.UploadMeshData(true);
        }
        
        private static void CreateQuadMesh()
        {
            quadMesh = new Mesh() { name = "Procedural HQuad Mesh" };

            quadMesh.vertices = new[]
            {
                new Vector3(-0.5f, 0, -0.5f),
                new Vector3(-0.5f, 0, 0.5f),
                new Vector3( 0.5f, 0, -0.5f),
                new Vector3( 0.5f, 0, 0.5f),
            };

            quadMesh.uv = new[]
            {
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 0),
                new Vector2(1, 1),
            };
            
            quadMesh.tangents = new[]
            {
                new Vector4(1, 0, 0, -1),
                new Vector4(1, 0, 0, -1),
                new Vector4(1, 0, 0, -1),
                new Vector4(1, 0, 0, -1),
            };
            
            quadMesh.normals = new[]
            {
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 0),
            };

            quadMesh.triangles = new[]
            {
                0, 1, 2,
                2, 1, 3
            };

            quadMesh.UploadMeshData(true);
        }
        
        private static void CreateCubeMesh()
        {
            cubeMesh = new()
            {
                name = "Procedural Cube"
            };

            cubeMesh.vertices = new[]
            {
                new Vector3( 0.50f, -0.50f, 0.50f),
                new Vector3(-0.50f, -0.50f, 0.50f),
                new Vector3( 0.50f, 0.50f,  0.50f),
                new Vector3(-0.50f, 0.50f,  0.50f),
                new Vector3( 0.50f, 0.50f,  -0.50f),
                new Vector3(-0.50f, 0.50f,  -0.50f),
                new Vector3( 0.50f, -0.50f, -0.50f),
                new Vector3(-0.50f, -0.50f, -0.50f),
                new Vector3( 0.50f, 0.50f,  0.50f),
                new Vector3(-0.50f, 0.50f,  0.50f),
                new Vector3( 0.50f, 0.50f,  -0.50f),
                new Vector3(-0.50f, 0.50f,  -0.50f),
                new Vector3( 0.50f, -0.50f, -0.50f),
                new Vector3( 0.50f, -0.50f, 0.50f),
                new Vector3(-0.50f, -0.50f, 0.50f),
                new Vector3(-0.50f, -0.50f, -0.50f),
                new Vector3(-0.50f, -0.50f, 0.50f),
                new Vector3(-0.50f, 0.50f,  0.50f),
                new Vector3(-0.50f, 0.50f,  -0.50f),
                new Vector3(-0.50f, -0.50f, -0.50f),
                new Vector3( 0.50f, -0.50f, -0.50f),
                new Vector3( 0.50f, 0.50f,  -0.50f),
                new Vector3( 0.50f, 0.50f,  0.50f),
                new Vector3( 0.50f, -0.50f, 0.50f),
            };

            cubeMesh.triangles = new[]
            {
                0 , 2 , 3 , 0 ,
                3 , 1 , 8 , 4 ,
                5 , 8 , 5 , 9 ,
                10, 6 , 7 , 10,
                7 , 11, 12, 13,
                14, 12, 14, 15,
                16, 17, 18, 16,
                18, 19, 20, 21,
                22, 20, 22, 23,
            };

            cubeMesh.UploadMeshData(true);
        }
    }
}