using System;
using System.IO;
using System.Data;
using System.Text;


namespace XCaliburTextReader {

    class Peak {
	public double Mass=0;
	public float  Intensity=0;
	public Peak   Next= null;

	public Peak() {
	}
	
	public Peak(double pMass, float pIntensity) {
	    Mass = pMass;
	    Intensity = pIntensity;
	}
    }
    
    class MS2Spectrum {
	public String FILE_NAME="";
	public int    SCANS=0;
	public float  RETENTION_TIME=0;
	public String ACTIVATION="HCD";
	public int    MS_ONE_ID=0;
	public int    MS_ONE_SCAN=0;
	public int    LEVEL=1;
	public double PRECURSOR_MZ=0;
	public int    PRECURSOR_CHARGE=0;
	public double PRECURSOR_MASS=0;
	public float  PRECURSOR_INTENSITY=0;
	public Peak   Peaks=new Peak();
	public MS2Spectrum Next= null;

	public MS2Spectrum () {
	}
	
	public MS2Spectrum (int pSCANS, String pFILE_NAME) {
	    SCANS =     pSCANS;
	    FILE_NAME = pFILE_NAME;
	}

	public Peak Find(double pTargetMass, double pMassThreshold) {
	    Peak   PRunner = Peaks.Next;
	    Peak   BestPeakSoFar = null;
	    double BestMassErrorSoFar = Double.MaxValue;
	    double ThisMassError;
	    while (PRunner != null) {
		ThisMassError = Math.Abs(PRunner.Mass - pTargetMass);
		if ( (ThisMassError < pMassThreshold) && (ThisMassError < BestMassErrorSoFar) ) {
		    BestPeakSoFar = PRunner;
		    BestMassErrorSoFar = ThisMassError;
		}
		PRunner = PRunner.Next;
	    }
	    return BestPeakSoFar;
	}
	
	public void DetectChargesFromMS1() {
	    //TODO: look in the MS preceding each MS/MS to find the deconvolved mass best relating to precursor m/z
	    //This code assumes MS1 scans are present and that all scans are reported in order of retention time, not separated by MS level.
	    MS2Spectrum Runner = this.Next;
	    MS2Spectrum LastMS1 = null;
	    Peak        BestPeak = null;
	    double      Proton = 1.007276466621;
	    double      TargetMass;
	    double      MassThreshold = 4.2;
	    double      BestMass;
	    float       BestIntensity;
	    int         BestCharge;
	    int         NoMatch = 0;
	    int         MS2Count = 0;
	    int         MaxCharge = 30;
	    int         ChargeLooper;
	    while (Runner != null) {
		if (Runner.LEVEL == 1) {
		    LastMS1 = Runner;
		}
		else if (Runner.LEVEL == 2) {
		    BestIntensity = 0;
		    BestCharge = 0;
		    BestMass = 0;
		    MS2Count++;
		    if (LastMS1 == null) {
			Console.Error.WriteLine("I encountered an MS/MS before I saw an MS!  Charge determination cannot proceed.");
			Environment.Exit(2);
		    }
		    for (ChargeLooper = 1; ChargeLooper <= MaxCharge; ChargeLooper++) {
			TargetMass = (Runner.PRECURSOR_MZ-Proton) * ChargeLooper;
			BestPeak = LastMS1.Find(TargetMass, MassThreshold);
			if ( (BestPeak != null) && (BestPeak.Intensity > BestIntensity) ) {
			    BestCharge = ChargeLooper;
			    BestMass = BestPeak.Mass;
			    BestIntensity = BestPeak.Intensity;
			}
		    }
		    if (BestIntensity > 0) {
			Runner.PRECURSOR_INTENSITY = BestIntensity;
			Runner.PRECURSOR_CHARGE = BestCharge;
			Runner.PRECURSOR_MASS = BestMass;
		    }
		    else {
			NoMatch++;
			Runner.PRECURSOR_INTENSITY = 0;
			Runner.PRECURSOR_CHARGE = 1;
			Runner.PRECURSOR_MASS = Runner.PRECURSOR_MZ;
		    }
		}
		Runner = Runner.Next;
	    }
	    Console.Error.WriteLine(NoMatch + " of " + MS2Count + " MS/MS scans lacked a precursor of charge <= " + MaxCharge + " within " + MassThreshold + " Da in preceding MS scans.");
	}
	
	public void PrintMSAlign(int pLEVEL) {
	    MS2Spectrum Runner = this.Next;
	    Peak PkRunner;
	    while (Runner != null) {
		if (Runner.LEVEL == pLEVEL) {
		    Console.WriteLine("BEGIN IONS");
		    Console.WriteLine("ID=1");
		    Console.WriteLine("FRACTION_ID=0");
		    Console.WriteLine("FILE_NAME=" + Runner.FILE_NAME);
		    Console.WriteLine("SCANS=" + Runner.SCANS);
		    Console.WriteLine("RETENTION_TIME=" + Runner.RETENTION_TIME);
		    Console.WriteLine("LEVEL=" + Runner.LEVEL);
		    Console.WriteLine("PRECURSOR_MZ=" + Runner.PRECURSOR_MZ);
		    Console.WriteLine("PRECURSOR_CHARGE=" + Runner.PRECURSOR_CHARGE);
		    Console.WriteLine("PRECURSOR_MASS=" + Runner.PRECURSOR_MASS);
		    Console.WriteLine("PRECURSOR_INTENSITY=" + Runner.PRECURSOR_INTENSITY);
		    PkRunner = Runner.Peaks.Next;
		    while (PkRunner != null) {
			Console.WriteLine(PkRunner.Mass + "\t" + PkRunner.Intensity + "\t1");
			PkRunner = PkRunner.Next;
		    }
		    Console.WriteLine("END IONS");
		    Console.WriteLine();
		}
		Runner = Runner.Next;
	    }
	}
    }

    class Program {
	static void Main(string[] args) {
	    if (args.Length==0) {
		Console.Error.WriteLine("Supply the name of the XCalibur text file for processing.");
		Environment.Exit(1);
	    }
	    string       FileString = args[0];
	    string       ThisLine;
	    string[]     ThisArray;
	    MS2Spectrum  ScansHead = new MS2Spectrum();
	    MS2Spectrum  ScansTail = ScansHead;
	    Peak         PkRunner = null;
	    StreamReader streamreader = new StreamReader(FileString);
	    char[]       delimiter = new char[] { ' ',',' };
	    while (streamreader.Peek() > 0) {
		ThisLine = streamreader.ReadLine();
		if (ThisLine.Length > 0) {
		    ThisArray=ThisLine.Split(delimiter);
		    /*
		      for (int looper = 0; looper < ThisArray.Length; looper++) {
			Console.WriteLine(looper + ": " + ThisArray[looper]);
		    }
		    */
		    try {
			if (ThisArray.Length > 1) {
			    if (ThisArray[0] == "ScanHeader") {
				ScansTail.Next = new MS2Spectrum(Convert.ToInt32(ThisArray[2]), FileString);
				ScansTail = ScansTail.Next;
				PkRunner = ScansTail.Peaks;
			    }
			    if (ThisArray[0] == "start_time") {
				ScansTail.RETENTION_TIME = Convert.ToSingle(ThisArray[2]);
			    }
			    if ((ThisArray[0] == "Polarity") && (ThisArray[10] == "MS2")) {
				ScansTail.LEVEL = 2;
			    }
			    if ((ThisArray[0] == "Precursor") && (ThisArray.Length>3)) {
				ScansTail.PRECURSOR_MZ = Convert.ToSingle(ThisArray[3]);
			    }
			    /*
			    if (ThisArray[0] == "uScanCount") {
				ScansTail.PRECURSOR_MASS = Convert.ToDouble(ThisArray[10]);
			    }
			    if (ThisArray[0] == "num_readings") {
				ScansTail.PRECURSOR_INTENSITY = Convert.ToSingle(ThisArray[6]);
			    }
			    */
			    if ((ThisArray[0] == "Packet") && (ThisArray[1] == "#")) {
				PkRunner.Next = new Peak(Convert.ToDouble(ThisArray[10]), Convert.ToSingle(ThisArray[6]));
				PkRunner = PkRunner.Next;
			    }
			}
		    } catch (IndexOutOfRangeException e) {
			Console.Error.WriteLine(ThisLine);
			Console.Error.WriteLine(ThisArray.Length);
			Console.Error.WriteLine(e);
		    }
		}
	    }
	    ScansHead.DetectChargesFromMS1();
	    ScansHead.PrintMSAlign(2);
	}
    }
}
