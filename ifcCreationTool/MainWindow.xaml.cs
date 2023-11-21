using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO;
using Xbim.Ifc4.SharedBldgElements;
using Xbim.Common;
using Xbim.Ifc4.Kernel;
using System.IO;
using Xbim.Ifc4.MeasureResource;
using Xbim.Ifc4.RepresentationResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.GeometricConstraintResource;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.PresentationAppearanceResource;
using Xbim.Ifc4.PresentationOrganizationResource;
using Xbim.Ifc4.MaterialResource;
using Xbim.Ifc4.ProfileResource;
using Xbim.Ifc4.DateTimeResource;
using Xbim.Ifc4.QuantityResource;
using Xbim.Ifc4.PropertyResource;
using Xbim.Ifc4.ExternalReferenceResource;
using Xbim.Ifc4.ActorResource;
using System.Windows.Media.Media3D;

namespace ifcCreationTool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            CreateModel();
        }

        private void CreateModel()
        {
            using (var model = CreateandInitModel("TestProject"))
            {
                if (model != null)
                {
                    IfcBuilding building = CreateBuilding(model, "Default Building");
                    IfcBeamStandardCase beam = CreateBeam(model, 250, 500); // radius, height 
                    IfcBeamStandardCase reducer = CreateReducer(model, 250, 125, 300, true); // eccentric reducer
                    IfcBeamStandardCase reducer2 = CreateReducer(model, 250, 125, 300); // concentric reducer
                    IfcBeamStandardCase elbow = CreateElbow(model, 300, 900); // pipe diameter, curve diameter

                    using (var txn = model.BeginTransaction("Add Wall"))
                    {
                        building.AddElement(beam);
                        building.AddElement(reducer);
                        building.AddElement(elbow);
                        building.AddElement(reducer2);
                        txn.Commit();
                    }

                    if (beam != null)
                    {
                        try
                        {
                            Trace.WriteLine("Standard Wall successfully created....");
                            // write the IFC File
                            // TODO : edit file path
                            var path = @"C:\Users\rseo0\source\repos\ifcCreationTool\ifcCreationTool";
                            model.SaveAs(path + @"\model.ifc");
                            Trace.WriteLine("model.ifc has been successfully written!");
                        }
                        catch (Exception e)
                        {
                            Trace.WriteLine("Failed to save model.ifc");
                            Trace.WriteLine(e.Message);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Failed to initialize the model");
                }
            }
        }

        private static IfcStore CreateandInitModel(string projectName)
        {
            //first we need to set up some credentials for ownership of data in the new model
            var credentials = new XbimEditorCredentials
            {
                ApplicationDevelopersName = "Ryan Seo",
                ApplicationFullName = "Test Application",
                ApplicationIdentifier = "test.exe",
                ApplicationVersion = "1.0",
                EditorsFamilyName = "Team",
                EditorsGivenName = "Test",
                EditorsOrganisationName = "Testing Team"
            };

            var model = IfcStore.Create(credentials, XbimSchemaVersion.Ifc4, XbimStoreType.InMemoryModel);

            //Begin a transaction as all changes to a model are ACID
            using (var txn = model.BeginTransaction("Initialise Model"))
            {
                var project = model.Instances.New<IfcProject>();
                //set the units to SI (mm and metres)
                project.Initialize(ProjectUnits.SIUnitsUK);
                project.Name = projectName;
                txn.Commit();
            }
            return model;
        }

        private static IfcBuilding CreateBuilding(IfcStore model, string name)
        {
            using (var txn = model.BeginTransaction("Create Building"))
            {
                var building = model.Instances.New<IfcBuilding>();
                building.Name = name;

                building.CompositionType = IfcElementCompositionEnum.ELEMENT;
                var localPlacement = model.Instances.New<IfcLocalPlacement>();
                building.ObjectPlacement = localPlacement;
                var placement = model.Instances.New<IfcAxis2Placement3D>();
                localPlacement.RelativePlacement = placement;
                placement.Location = model.Instances.New<IfcCartesianPoint>(p => p.SetXYZ(0, 0, 0));
                // get the project, there should only be one and it should exist
                var project = model.Instances.OfType<IfcProject>().FirstOrDefault();
                project?.AddBuilding(building);
                txn.Commit();
                return building;
            }
        }

        static private IfcBeamStandardCase CreateBeam(IfcStore model, double diameter, double height)
        {
            var radius = diameter / 2;

            using (var txn = model.BeginTransaction("Create Beam"))
            {
                var beam = model.Instances.New<IfcBeamStandardCase>();
                beam.Name = "A Standard beam";

                var beamProf = model.Instances.New<IfcCircleHollowProfileDef>();
                beamProf.ProfileType = IfcProfileTypeEnum.AREA;
                beamProf.Radius = radius;
                beamProf.WallThickness = 10;
                var insertPoint = model.Instances.New<IfcCartesianPoint>();
                insertPoint.SetXY(0, 400); //insert at arbitrary position
                beamProf.Position = model.Instances.New<IfcAxis2Placement2D>();
                beamProf.Position.Location = insertPoint;

                IfcCircleHollowProfileDef endBeamProf;
                endBeamProf = beamProf;

                // model as a swept area solid
                var body = model.Instances.New<IfcExtrudedAreaSolidTapered>();
                body.Depth = height;
                body.SweptArea = beamProf;
                body.EndSweptArea = endBeamProf;
                body.ExtrudedDirection = model.Instances.New<IfcDirection>();
                body.ExtrudedDirection.SetXYZ(0, 0, 1);

                // TODO: Geometry Positioning
                var origin = model.Instances.New<IfcCartesianPoint>();
                origin.SetXYZ(50, 50, 50);
                body.Position = model.Instances.New<IfcAxis2Placement3D>();
                body.Position.Location = origin;

                // Create a Definition shape to hold the geometry
                var shape = model.Instances.New<IfcShapeRepresentation>();
                var modelContext = model.Instances.OfType<IfcGeometricRepresentationContext>().FirstOrDefault();
                shape.ContextOfItems = modelContext;
                shape.RepresentationType = "SweptSolid";
                shape.RepresentationIdentifier = "Body";
                shape.Items.Add(body);

                // Create a Product Definition and add the model geometry to the wall
                var rep = model.Instances.New<IfcProductDefinitionShape>();
                rep.Representations.Add(shape);
                beam.Representation = rep;

                // Place the beam into the model
                var ax3D = model.Instances.New<IfcAxis2Placement3D>();
                ax3D.Location = origin;
                ax3D.RefDirection = model.Instances.New<IfcDirection>();
                ax3D.RefDirection.SetXYZ(1, 0, 0);
                ax3D.Axis = model.Instances.New<IfcDirection>();
                ax3D.Axis.SetXYZ(0,-1, 0);

                var lp = model.Instances.New<IfcLocalPlacement>();
                lp.RelativePlacement = ax3D; // LocalPlacement of object has a IFCAxis2Placement3D
                beam.ObjectPlacement = lp;

                txn.Commit();
                return beam;
            }
        }

        static private IfcBeamStandardCase CreateReducer(IfcStore model, double diameterA, double diameterB, double height, bool eccentric = false)
        {
            var radiusA = diameterA / 2;
            var radiusB = diameterB / 2;

            using (var txn = model.BeginTransaction("Create Beam"))
            {
                var beam = model.Instances.New<IfcBeamStandardCase>();
                beam.Name = "A Standard beam";

                var beamProf = model.Instances.New<IfcCircleHollowProfileDef>();
                beamProf.ProfileType = IfcProfileTypeEnum.AREA;
                beamProf.Radius = radiusA;
                beamProf.WallThickness = 10;
                var insertPoint = model.Instances.New<IfcCartesianPoint>();
                insertPoint.SetXY(0, 400); //insert at arbitrary position
                beamProf.Position = model.Instances.New<IfcAxis2Placement2D>();
                beamProf.Position.Location = insertPoint;

                IfcCircleHollowProfileDef endBeamProf;
                endBeamProf = model.Instances.New<IfcCircleHollowProfileDef>();
                endBeamProf.ProfileType = IfcProfileTypeEnum.AREA;
                endBeamProf.Radius = radiusB;
                endBeamProf.WallThickness = 10;
                endBeamProf.Position = model.Instances.New<IfcAxis2Placement2D>();
                endBeamProf.Position.Location = insertPoint;

                // model as a swept area solid
                var body = model.Instances.New<IfcExtrudedAreaSolidTapered>();
                body.Depth = height;
                body.SweptArea = beamProf;
                body.EndSweptArea = endBeamProf;
                body.ExtrudedDirection = model.Instances.New<IfcDirection>();

                // logic for eccentric reducer, creating vector of from center of A to center of B.
                // Not tested if radius B > radius A, only tested if radius A > radius B.
                // The vector direction goes from beamProf -> endBeamProf (SweptArea -> EndSweptArea)
                var y = eccentric ? Math.Abs(radiusA - radiusB) / height : 0;
                body.ExtrudedDirection.SetXYZ(0, y, 1);

                // parameters to insert the geometry in the model
                // TODO: Set position
                var origin = model.Instances.New<IfcCartesianPoint>();
                if (eccentric) origin.SetXYZ(-100, 50, 50); else origin.SetXYZ(-250, 50, 50);
                body.Position = model.Instances.New<IfcAxis2Placement3D>();
                body.Position.Location = origin;

                // Create a Definition shape to hold the geometry
                var shape = model.Instances.New<IfcShapeRepresentation>();
                var modelContext = model.Instances.OfType<IfcGeometricRepresentationContext>().FirstOrDefault();
                shape.ContextOfItems = modelContext;
                shape.RepresentationType = "SweptSolid";
                shape.RepresentationIdentifier = "Body";
                shape.Items.Add(body);

                // Create a Product Definition and add the model geometry to the wall
                var rep = model.Instances.New<IfcProductDefinitionShape>();
                rep.Representations.Add(shape);
                beam.Representation = rep;

                // now place the beam into the model
                var lp = model.Instances.New<IfcLocalPlacement>();
                var ax3D = model.Instances.New<IfcAxis2Placement3D>();
                ax3D.Location = origin;
                ax3D.RefDirection = model.Instances.New<IfcDirection>();
                ax3D.RefDirection.SetXYZ(1, 0, 0);
                ax3D.Axis = model.Instances.New<IfcDirection>();
                ax3D.Axis.SetXYZ(0, -1, 0);
                lp.RelativePlacement = ax3D; // LocalPlacement of object has a IFCAxis2Placement3D
                beam.ObjectPlacement = lp;

                txn.Commit();
                return beam;
            }
        }

        static private IfcBeamStandardCase CreateElbow(IfcStore model, double pipeDiameter, double curveDiameter)
        {
            var pipeRadius = pipeDiameter / 2;
            var curveRadius = curveDiameter / 2;

            using (var txn = model.BeginTransaction("Create Beam"))
            {
                var elbow = model.Instances.New<IfcBeamStandardCase>();
                elbow.Name = "An elbow";

                // model as a swept area solid
                var body = model.Instances.New<IfcSweptDiskSolid>();
                // https://standards.buildingsmart.org/IFC/RELEASE/IFC4/ADD1/HTML/schema/ifcgeometricmodelresource/lexical/ifcsweptdisksolid.htm
                // Defines dimensions of ellipse (in this case, a circle).
                var ellipse = model.Instances.New<IfcEllipse>(p => 
                {   p.Position = model.Instances.New<IfcAxis2Placement3D>(a => 
                    {   a.Location = model.Instances.New<IfcCartesianPoint>(c => c.SetXYZ(0, 0, 0)); 
                        a.RefDirection = model.Instances.New<IfcDirection>(d => d.SetXYZ(1, 0, 0)); 
                        a.Axis = model.Instances.New<IfcDirection>(d => d.SetXYZ(0, 0, 1)); 
                    }); p.SemiAxis1 = curveRadius; p.SemiAxis2 = curveRadius; 
                });

                // http://standards.buildingsmart.org/IFC/DEV/IFC4_2/FINAL/HTML/schema/ifcgeometryresource/lexical/ifctrimmedcurve.htm
                // In this case, we trim off 270 degrees of a circle, leaving just a 90 degree elbow.
                var curve = model.Instances.New<IfcTrimmedCurve>(t => 
                { t.BasisCurve = ellipse;
                  t.Trim1.Add(new IfcParameterValue(3*Math.PI / 2)); // value being trimmed
                  t.Trim2.Add(new IfcParameterValue(0));
                  t.MasterRepresentation = IfcTrimmingPreference.PARAMETER;
                  t.SenseAgreement = true; 
                });

                body.Directrix = curve;
                body.Radius = pipeRadius;

                // parameters to insert the geometry in the model
                var origin = model.Instances.New<IfcCartesianPoint>();
                origin.SetXYZ(0, 0, 0);

                // Create a Definition shape to hold the geometry
                var shape = model.Instances.New<IfcShapeRepresentation>();
                var modelContext = model.Instances.OfType<IfcGeometricRepresentationContext>().FirstOrDefault();
                shape.ContextOfItems = modelContext;
                shape.RepresentationType = "SweptSolid";
                shape.RepresentationIdentifier = "Body";
                shape.Items.Add(body);

                // Create a Product Definition and add the model geometry to the wall
                var rep = model.Instances.New<IfcProductDefinitionShape>();
                rep.Representations.Add(shape);
                elbow.Representation = rep;

                // place the beam into the model
                var lp = model.Instances.New<IfcLocalPlacement>();
                var ax3D = model.Instances.New<IfcAxis2Placement3D>();
                ax3D.Location = origin;
                ax3D.RefDirection = model.Instances.New<IfcDirection>();
                ax3D.RefDirection.SetXYZ(0, 1, 0);
                ax3D.Axis = model.Instances.New<IfcDirection>();
                ax3D.Axis.SetXYZ(0, 0, 1);
                lp.RelativePlacement = ax3D;
                elbow.ObjectPlacement = lp;

                txn.Commit();
                return elbow;
            }
        }
    }

}
