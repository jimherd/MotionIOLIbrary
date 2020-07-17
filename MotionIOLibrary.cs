using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO.Ports;

namespace MotionIOLibrary
{
    public class FPGA_uP_IO
    {

        //***********************************************************************
        // Globals
        //***********************************************************************

        public int nos_PWM_channels = -1;
        public int nos_QE_channels  = -1;
        public int nos_RC_channels  = -1;

        public uint PWM_base = 1;
        public uint QE_base  = 0;
        public uint RC_base  = 0;

        //***********************************************************************
        // Constant definitions
        //***********************************************************************

        const int COMBAUD      = 256000;  // default baud rate
        const int READ_TIMEOUT = 10000;   // timeout for read reply (10 seconds)

        public enum ErrorCode {
            NO_ERROR = 0,
            BAD_COMPORT_OPEN = -100,
            UNKNOWN_COM_PORT = -101,
            BAD_COMPORT_READ = -102,
        }

        //***********************************************************************
        // Objects
        //***********************************************************************

        static SerialPort _serialPort;

        //*********************************************************************
        // constructor
        //*********************************************************************
        public FPGA_uP_IO() {
            
            _serialPort = new SerialPort(); // Create a new SerialPort object
        }

        //***********************************************************************
        // Methods
        //***********************************************************************
        // Init_comms : Initialise specified serial COM port
        // ==========

        public FPGA_uP_IO.ErrorCode Init_comms(string COM_port, int baud) {
            FPGA_uP_IO.ErrorCode status;

            status = ErrorCode.NO_ERROR;
            _serialPort.BaudRate = baud;
            if (string.IsNullOrEmpty(COM_port)) {
                return ErrorCode.UNKNOWN_COM_PORT;
            }
            _serialPort.PortName = COM_port;

            try {
                _serialPort.Open();
            }
            catch {
                status = ErrorCode.BAD_COMPORT_OPEN;
            }
            return status;
        }

        //***********************************************************************
        // do_command : execute remote command and get reply
        // ==========
        public FPGA_uP_IO.ErrorCode do_command(string command) {

            _serialPort.WriteLine(command);
            FPGA_uP_IO.ErrorCode status = get_reply();

            return status;
        }

        //*********************************************************************** 
        // get_reply : Read a status/data reply from LLcontrol subsystem
        // =========

        public FPGA_uP_IO.ErrorCode get_reply() {
            string reply;
            ErrorCode status = ErrorCode.NO_ERROR;

            //serialPort1.DiscardInBuffer();
            _serialPort.ReadTimeout = READ_TIMEOUT;
            try {
                reply = _serialPort.ReadLine();
            }
            catch (TimeoutException) {
                status = ErrorCode.BAD_COMPORT_READ;
            }
            return status;
        }
    }
}
