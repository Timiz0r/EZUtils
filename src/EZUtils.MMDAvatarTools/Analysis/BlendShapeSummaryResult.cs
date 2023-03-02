namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;

    public class BlendShapeSummaryResult
    {
        public BlendShapeDefinition Definition { get; }
        //of set of name and synonyms
        public IReadOnlyList<string> ExistingBlendShapes { get; }

        public BlendShapeSummaryResult(BlendShapeDefinition definition, IReadOnlyList<string> existingBlendShapes)
        {
            Definition = definition;
            ExistingBlendShapes = existingBlendShapes;
        }
    }
}
