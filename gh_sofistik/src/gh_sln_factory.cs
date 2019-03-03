﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace gh_sofistik
{
   // class implementing a GH_ container for Rhino.Geometry.Curve
   public class GS_StructuralLine : GH_GeometricGoo<Curve>, IGH_PreviewData, IGH_BakeAwareData, IGS_StructuralElement
   {
      public int Id { get; set; } = 0;
      public int GroupId { get; set; } = 0;    
      public int SectionId { get; set; } = 0;
      public Vector3d DirectionLocalZ { get; set; } = new Vector3d();

      private SupportCondition _supp_condition = null;

      private LocalFrameVisualisation _localFrame = new LocalFrameVisualisation();

      private string fixLiteral = string.Empty;

      public string FixLiteral
      {
         get
         {
            return fixLiteral;
         }
         set
         {
            fixLiteral = value;
            _supp_condition = new SupportCondition(fixLiteral);
         }
      }

      public override BoundingBox Boundingbox
      {
         get { return Value.GetBoundingBox(true); }
      }

      public override string TypeDescription
      {
         get { return Value.ToString() + " with Structural Properties"; }
      }

      public override string TypeName
      {
         get { return "GS_StructuralLine"; }
      }

      public override string ToString()
      {
         return "Structural Line, Id = " + Id.ToString();
      }

      public override IGH_GeometricGoo DuplicateGeometry()
      {
         return new GS_StructuralLine()
         {
            Value = this.Value.DuplicateCurve(),
            Id = this.Id,
            GroupId = this.GroupId,
            SectionId = this.SectionId,
            DirectionLocalZ = this.DirectionLocalZ,
            FixLiteral = this.FixLiteral
         };
      }

      public override BoundingBox GetBoundingBox(Transform xform)
      {
         return xform.TransformBoundingBox(Value.GetBoundingBox(true));
      }

      public override bool CastTo<Q>(out Q target)
      {
         return Util.CastCurveTo(this.Value, out target);
      }

      public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
      {
         var dup = this.DuplicateGeometry() as GS_StructuralLine;
         xmorph.Morph(dup.Value);

         return dup;
      }

      public override IGH_GeometricGoo Transform(Transform xform)
      {
         var dup = this.DuplicateGeometry() as GS_StructuralLine;
         dup.Value.Transform(xform);

         return dup;
      }

      public BoundingBox ClippingBox
      {
         get
         {
            if (fixLiteral.Equals(""))
               return Value.GetBoundingBox(false);
            else
               return DrawUtil.GetClippingBoxSupports(Value.GetBoundingBox(false));
         }
      }

      public void DrawViewportWires(GH_PreviewWireArgs args)
      {
         //ClippingBox
         //args.Pipeline.DrawBox(ClippingBox, System.Drawing.Color.Black);
         if (Value != null)
         {            
            System.Drawing.Color colStr = args.Color;
            System.Drawing.Color colSup = args.Color;
            if (!DrawUtil.CheckSelection(colStr))
            {
               colStr = DrawUtil.DrawColorStructuralElements;
               colSup = System.Drawing.Color.Black;
            }
            else
               drawLocalFrame(args.Pipeline);

            args.Pipeline.DrawCurve(Value, colStr, args.Thickness+1);

            drawSupportLine(args.Pipeline, colSup, false);            
         }
      }

      public void DrawViewportMeshes(GH_PreviewMeshArgs args)
      {
         // no need to draw meshes
         if (Value != null)
         {
            drawSupportLine(args.Pipeline, DrawUtil.DrawColorSupports, true);
         }
      }

      private void drawLocalFrame(Rhino.Display.DisplayPipeline pipeline)
      {
         if (DrawUtil.ScaleFactorLocalFrame > 0.0001)
         {
            if (!_localFrame.isValid)
            {
               updateLocalFrameTransforms();
            }
            _localFrame.Draw(pipeline);
         }
      }

      private void updateLocalFrameTransforms()
      {
         _localFrame.Transforms.Clear();

         var dz = DirectionLocalZ.IsTiny() ? -1 * Vector3d.ZAxis : DirectionLocalZ;

         _localFrame.Transforms.AddRange(DrawUtil.GetCurveTransforms(Value, true, dz, null, DrawUtil.ScaleFactorLocalFrame, DrawUtil.DensityFactorLocalFrame));
      }

      private void drawSupportLine(Rhino.Display.DisplayPipeline pipeline, System.Drawing.Color col, bool shaded)
      {
         if (DrawUtil.ScaleFactorSupports > 0.0001)
         {
            if (!_supp_condition.isValid)
            {
               updateSupportTransforms();
            }
            _supp_condition.Draw(pipeline, col, shaded);
         }
      }

      private void updateSupportTransforms()
      {
         _supp_condition.Transforms.Clear();

         var dz = DirectionLocalZ.IsTiny() ? Vector3d.ZAxis : DirectionLocalZ;

         _supp_condition.Transforms.AddRange(DrawUtil.GetCurveTransforms(Value, _supp_condition.LocalFrame, dz, null, DrawUtil.ScaleFactorSupports, DrawUtil.DensityFactorSupports));
      }

      public bool BakeGeometry(RhinoDoc doc, ObjectAttributes baking_attributes, out Guid obj_guid)
      {
         if (Value != null)
         {
            var att = baking_attributes.Duplicate();

            var str_id = this.Id > 0 ? Id.ToString() : "0"; // string.Empty;

            string fix_literal = this.FixLiteral
               .Replace("PP", "PXPYPZ")
               .Replace("MM", "MXMYMZ");

            if (fix_literal == "F")
               fix_literal = "PXPYPZMXMYMZ";

            // set user strings
            att.SetUserString("SOF_OBJ_TYPE", "SLN");
            att.SetUserString("SOF_ID", str_id);

            if(this.GroupId > 0)
               att.SetUserString("SOF_GRP", this.GroupId.ToString());
            if(this.SectionId > 0)
            {
               att.SetUserString("SOF_STYP", "B");
               att.SetUserString("SOF_STYP2", "E");
               att.SetUserString("SOF_SNO", this.SectionId.ToString());
               att.SetUserString("SOF_SNOE", "SOF_PROP_COMBO_NONE");
            }
            att.SetUserString("SOF_SDIV", "0.0");

            if(DirectionLocalZ.Length > 1.0E-6)
            {
               att.SetUserString("SOF_DRX", DirectionLocalZ.X.ToString("F6"));
               att.SetUserString("SOF_DRY", DirectionLocalZ.Y.ToString("F6"));
               att.SetUserString("SOF_DRZ", DirectionLocalZ.Z.ToString("F6"));
            }

            if (string.IsNullOrEmpty(fix_literal) == false)
               att.SetUserString("SOF_FIX", fix_literal);

            obj_guid = doc.Objects.AddCurve(Value, att);
         }
         else
         {
            obj_guid = new Guid();
         }
         return true;
      }
   }

   // create structural line
   public class CreateStructuralLine : GH_Component
   {
      public CreateStructuralLine()
         : base("Structural Line","Structural Line","Creates SOFiSTiK Structural Lines","SOFiSTiK", "Structure")
      {}

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.structural_line16; }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddCurveParameter("Curve", "Crv", "Curve Geometry", GH_ParamAccess.list);
         pManager.AddIntegerParameter("Number", "Number", "Identifier of structural line", GH_ParamAccess.list, 0);
         pManager.AddIntegerParameter("Group", "Group", "Group number of structural line", GH_ParamAccess.list, 0);
         pManager.AddIntegerParameter("Section", "Section", "Identifier of cross section", GH_ParamAccess.list, 0);
         pManager.AddVectorParameter("Dir Z", "Dir z", "Direction of local z-axis", GH_ParamAccess.list, new Vector3d());
         pManager.AddTextParameter("Fixation", "Fixation", "Support condition literal", GH_ParamAccess.list, string.Empty);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGeometryParameter("Structural Line", "Sln", "SOFiSTiK Structural Line", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var curves = da.GetDataList<Curve>(0);
         var identifiers = da.GetDataList<int>(1);
         var groups = da.GetDataList<int>(2);
         var sections = da.GetDataList<int>(3);
         var zdirs = da.GetDataList<Vector3d>(4);
         var fixations = da.GetDataList<string>(5); 

         var gh_structural_curves = new List<GS_StructuralLine>();

         for( int i=0; i<curves.Count; ++i)
         {
            var c = curves[i];

            var gc = new GS_StructuralLine()
            {
               Value = c,
               Id = identifiers.GetItemOrCountUp(i),
               GroupId = groups.GetItemOrLast(i),
               SectionId = sections.GetItemOrLast(i),
               DirectionLocalZ = zdirs.GetItemOrLast(i),
               FixLiteral = fixations.GetItemOrLast(i)
            };
            gh_structural_curves.Add(gc);
         }

         da.SetDataList(0, gh_structural_curves);
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("743DEE7B-D30B-4286-B74B-1415B92E87F7"); }
      }
   }
}
