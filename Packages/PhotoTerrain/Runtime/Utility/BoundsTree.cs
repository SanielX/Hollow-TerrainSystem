using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;
using UnityEngine;

namespace Hollow.TerrainSystem.Utility
{
internal class BoundsTree<T>
{
    public BoundsTree(int maxNodes)
    {
        nodesPool = new BoundsNode[maxNodes];
        freeNodes = new int[maxNodes];
    }

    private int          nodesCount;
    private BoundsNode[] nodesPool;

    private int[] freeNodes;
    private int   freeNodesCount;

    public  int   root = -1;

    public void Clear()
    {
        nodesCount = 0;
        freeNodesCount = 0;
        System.Array.Clear(nodesPool, 0, nodesPool.Length);
    }

    public ref BoundsNode at(int index) => ref nodesPool[index];

    public void UpdateBounds(ref BoundsNode node)
    {
        if (node.left < 0 && node.right >= 0)
            node.bounds = at(node.right).bounds;
        else if (node.left >= 0 && node.right < 0)
            node.bounds = at(node.left).bounds;
        else
            node.bounds = UBoundsUtility.Combine(at(node.left).bounds, at(node.right).bounds);
    }

    public struct BoundsNode
    {
        public UBounds bounds;
        public T       value;

        public int parent, left, right;

        public bool IsLeaf => left < 0 && right < 0;
    }

    public void Insert(T obj, UBounds bounds) => Insert(ref root, obj, bounds);

    public void Remove(int nodeIdx)
    {
        if (nodeIdx < 0)
            return;

        var parentIdx  = at(nodeIdx).parent;
        ref var parentNode = ref at(parentIdx);

        if (at(nodeIdx).parent < 0)
        {
            ref var parentRef = ref (parentNode.left == nodeIdx ? ref parentNode.left : ref parentNode.right);
            parentRef = -1;

            free(nodeIdx);

            UpdateBoundsUpwards(parentIdx);
            return;
        }

        var otherNode = parentNode.left == nodeIdx ? parentNode.right : parentNode.left;

        var grandParent = parentNode.parent;
        ref var grandParentNode = ref at(grandParent);

        ref var referenceToChange = ref (grandParentNode.left == parentIdx ? ref grandParentNode.left : ref grandParentNode.right);
        referenceToChange = otherNode;

        at(otherNode).parent = grandParent;

        free(parentIdx);
        free(nodeIdx);

        UpdateBoundsUpwards(otherNode);
    }

    public void FindIntersectionsWith(UBounds bounds, List<T> results)
    {
        results.Clear();
        addFromNode(root);

        void addFromNode(int nodeIdx)
        {
            if (nodeIdx < 0)
                return;

            ref var node = ref at(nodeIdx);
            if (node.bounds.Overlaps(bounds))
            {
                if (node.left < 0 && node.right < 0)
                {
                    results.Add(node.value);
                    return;
                }

                addFromNode(node.left);
                addFromNode(node.right);
            }
        }
    }

    public void FindIntersectionsWith(float4 sphere, List<int> results)
    {
        results.Clear();
        addFromNode(root);

        void addFromNode(int nodeIdx)
        {
            if (nodeIdx < 0)
                return;

            if (at(nodeIdx).bounds.InteresectsSphere(sphere))
            {
                // Only add leaf nodes
                if (at(nodeIdx).left < 0 && at(nodeIdx).right < 0)
                {
                    results.Add(nodeIdx);
                    return;
                }

                addFromNode(at(nodeIdx).left);
                addFromNode(at(nodeIdx).right);
            }
        }
    }

    int alloc()
    {
        if (freeNodesCount > 0)
        {
            int idx = freeNodes[freeNodesCount - 1];
            freeNodesCount--;
            clear(idx);

            return idx;
        }

        clear(nodesCount);
        return nodesCount++;
    }

    void clear(int idx)
    {
        nodesPool[idx].parent = -1;
        nodesPool[idx].left   = -1;
        nodesPool[idx].right   = -1;
        nodesPool[idx].bounds = default;
        nodesPool[idx].value = default;
    }

    void free(int idx)
    {
        if (idx == nodesCount - 1)
        {
            nodesCount--;
        }
        else
        {
            freeNodes[freeNodesCount] = idx;
            freeNodesCount++;
        }
    }

    void Insert(ref int root, T obj, UBounds bounds)
    {
        int insertNode = alloc();
        at(insertNode).bounds = bounds;
        at(insertNode).value = obj;

        if (root < 0)
        {
            root = insertNode;
            return;
        }

        if (at(root).left < 0 && at(root).right < 0)
        {
            var newParent = alloc();
            at(newParent).parent = at(root).parent;
            at(newParent).left   = root;
            at(newParent).right  = insertNode;

            at(root).parent = newParent;
            at(insertNode).parent = newParent;

            if (at(newParent).parent >= 0)
            {
                if (at(at(newParent).parent).left == root)
                    at(at(newParent).parent).left = newParent;
                else
                    at(at(newParent).parent).right = newParent;
            }
            else
                this.root = newParent;

            UpdateBoundsUpwards(insertNode);

            return;
        }

        var parent = root;

        while (at(parent).left >= 0 || at(parent).right >= 0)
        {
            var leftWeight  = getCost(at(parent).left);
            var rightWeight = getCost(at(parent).right);

            if (leftWeight < rightWeight || (leftWeight == Mathf.Infinity && rightWeight == Mathf.Infinity))
                parent = at(parent).left;
            else
                parent = at(parent).right;

            float getCost(int target)
            {
                if (target < 0)
                    return Mathf.Infinity;

                var originalVolume = at(target).bounds.volume();
                var newVolume      = UBoundsUtility.Combine(at(insertNode).bounds, at(target).bounds).volume();

                return newVolume - originalVolume;
            }
        }

        Insert(ref parent, obj, bounds);
    }

    void UpdateBoundsUpwards(int startNode)
    {
        var node = at(startNode).parent;

        while (node >= 0)
        {
            UpdateBounds(ref at(node));
            node = at(node).parent;
        }
    }

    public void DrawGizmo()
    {
        var col = Gizmos.color;

        Gizmos.color = Gizmos.color.WithAlpha(0.4f);

        if (root >= 0)
            drawNode(at(root));

        Gizmos.color = col;

        void drawNode(BoundsNode node)
        {
            Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);

            if (node.left >= 0)
            {
                Gizmos.color = Color.HSVToRGB(0, 1, 1).WithAlpha(0.5f);
                drawNode(at(node.left));
            }

            if (node.right >= 0)
            {
                Gizmos.color = Color.HSVToRGB(0.5f, 1, 1).WithAlpha(0.5f);
                drawNode(at(node.right));
            }
        }
    }

    public override string ToString()
    {
        StringBuilder builder = new StringBuilder(50);
        appendNode(root, 0);

        return builder.ToString();

        void appendNode(int nodeIdx, int intend)
        {
            bool colorRow = nodeIdx >= 0 && at(nodeIdx).IsLeaf;
            if (colorRow)
            {
                builder.Append("<color=#FFFFFF>");
            }

            builder.Append("<b>");
            for (int i = 0; i < intend; i++)
            {
                builder.Append("--");
            }

            builder.Append("></b>");

            if (nodeIdx < 0)
            {
                builder.Append("<b> NULL NODE </b>\n");
            }
            else
            {
                builder.Append(' ');

                if (at(nodeIdx).IsLeaf)
                    builder.Append(at(nodeIdx).value is null ? "Null" : at(nodeIdx).value.ToString());
                else
                    builder.Append("Folder:");

                if (colorRow)
                    builder.Append("</color>\n");
                else
                    builder.Append('\n');

                if (!at(nodeIdx).IsLeaf)
                {
                    appendNode(at(nodeIdx).left,  intend + 1);
                    appendNode(at(nodeIdx).right, intend + 1);
                }
            }
        }
    }
}
}