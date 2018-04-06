using Microsoft.CodeAnalysis;

namespace MappingGenerator
{
    static class  SyntaxHelper
    {
        public static T FindContainer<T>(this SyntaxNode tokenParent) where T:SyntaxNode
        {
            if (tokenParent is T invocation)
            {
                return invocation;
            }

            return tokenParent.Parent == null ? null : FindContainer<T>(tokenParent.Parent);
        } 
        
        public static SyntaxNode FindNearestContainer<T1, T2>(this SyntaxNode tokenParent) where T1:SyntaxNode where T2:SyntaxNode
        {
            if (tokenParent is T1 t1)
            {
                return t1;
            }
            
            if (tokenParent is T2 t2)
            {
                return t2;
            }
            

            return tokenParent.Parent == null ? null : FindNearestContainer<T1, T2>(tokenParent.Parent);
        }
    }
}
