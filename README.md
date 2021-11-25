# UA Nodeset Web Viewer
UA Nodeset Web Viewer is a tool used in Industrial IoT scenarios to bridge the gap from OT to IT. OPC UA is the standard interface for vendor-neutral Operational Technology (OT) interoperability in factories, plants and renewable energy farms with best-in-class data/information modeling functionality. The file format for these information models is called "node sets". As such, it defines the industrial digital twin, also known as the smart manufacturing profile. The OPC Foundation and CESMII have worked hard over the last year to make these information models/smart manufacturing profiles available online leveraging the new UA Cloud Library. The UA Nodeset Web Viewer can upload (and later download) these OPC UA information models to the UA Cloud Library.

The Plattform Industrie 4.0 in Europe has defined the industrial digital twin slightly broader, not only defining the OT digital twin, but the entire digital asset/product along its value chain, i.e. from design to manufacturing to operation to recycling. They call this industrial digital twin the Asset Administration Shell (AAS). The UA Nodeset Web Viewer can package OT digital twins into Asset Administration Shells, leveraging the AAS exchange format AASX (based on Open Office XML).

Microsoft has defined the IT digital twin using the Digital Twin Definition Language (DTDL). It also runs a cloud service leveraging these DTDL-based digital twins for analytics called the Microsoft Azure Digital Twins (ADT) service. The UA Nodeset Web Viewer can map OT digital twins to DTDL definitions and then upload them to ADT service instances.

Additional features of the UA Nodeset Web Viewer include the ability to run in a Docker container for easy deployment and maintenance and comes with a Web user interface. Several OPC UA nodeset files and can be loaded at once and then browsed. the tool is very useful for looking at the standardized node set files defined in the OPC UA companion specifications by the German machine builders association VDMA.

## Usage

Docker containers are automatically built. Simply run the app via:
docker run -p80:80 ghcr.io/digitaltwinconsortium/uanodesetwebviewer:main
And then point your browser to http://localhost

###  Upload 

![Start](Docs/Start.png)

- Open your OPC UA NodeSet file. (NOTE: Dependent NodeSet files need to be opened together.)


### Browsing

![Browsing](Docs/Sample.png)

- You can browse and interact with the model.
- Currently `READ` and `WRITE` of a node is possible.
