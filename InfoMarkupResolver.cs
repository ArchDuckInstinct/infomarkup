using System;
using System.Collections.Generic;

namespace InfoMarkup
{
    // ========================================================================================================================================
    // InfoMarkupResolver
    // TODO: Come up with better typenames than 'A' and 'B'
    // ========================================================================================================================================

    public class InfoMarkupResolver<A, B> where A : class
    {
        // ====================================================================================================================================
        // Resolver Method
        // ====================================================================================================================================

        public delegate void ResolveMethod(A instance, B linked);
        private ResolveMethod onResolve;

        // ====================================================================================================================================
        // Data
        // ====================================================================================================================================

        private class Node
        {
            public Node next;
            public B value;

            public Node(Node pNext, B pValue)
            {
                next    = pNext;
                value   = pValue;
            }
        }

        private class Item
        {
            public Node node;
            public A value;

            public Item(Node pNode, A pValue)
            {
                node    = pNode;
                value   = pValue;
            }
        }

        private Dictionary<string, Item> map;

        // ====================================================================================================================================
        // Constructor
        // ====================================================================================================================================

        public InfoMarkupResolver(ResolveMethod resolve)
        {
            map         = new Dictionary<string, Item>();
            onResolve   = resolve;
        }

        // ====================================================================================================================================
        // API
        // ====================================================================================================================================

        public A Link(string key, B target)
        {
            Item item;

            if (map.TryGetValue(key, out item))
            {
                if (item.value != null)
                    return item.value;

                item.node = new Node(item.node, target);
            }
            else
                map.Add(key, new Item(new Node(null, target), null));

            return null;
        }

        public void Resolve(string key, A instance)
        {
            Item item;

            if (map.TryGetValue(key, out item))
            {
                Node node = item.node;

                while (node != null)
                {
                    onResolve(instance, node.value);
                    node = node.next;
                }
            }
            else
                map.Add(key, new Item(null, instance));
        }

        public A Get(string key)
        {
            Item item;

            return map.TryGetValue(key, out item) ? item.value : null;
        }
    }
}