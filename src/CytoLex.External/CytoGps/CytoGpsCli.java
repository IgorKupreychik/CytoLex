// CytoLex wrapper around the CytoGPS batch JSON writer (T-049). Our own code, not part of CytoGPS.
// It makes the same call as CytoGPS's main/Run.java, with the paths taken from the command line:
//   java -cp <classes>;<antlr jar>;<javax.json jar> CytoGpsCli <input.txt> <output.json>
// Input: UTF-8 text, one karyotype per line. Output: CytoGPS_Result.json schema (docs/research/cytogps.md §3).
import java.nio.file.Paths;
import toolkit.AggregateJsonForBatchFile;

public class CytoGpsCli {
    public static void main(String[] args) throws Exception {
        if (args.length != 2) {
            System.err.println("usage: CytoGpsCli <input.txt> <output.json>");
            System.exit(2);
        }
        AggregateJsonForBatchFile.generateJson(Paths.get(args[0]), Paths.get(args[1]));
    }
}
