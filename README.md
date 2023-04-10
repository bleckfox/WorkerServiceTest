## Create WebAPI with worker service.
Worker service make request to endpoint and give data. Then save it to special receive file. 
When size of this file is over then we define in appsettings, it stopped requests. Then read receive file, sort it, and then safe result into sorted file.
#### note: receive and sorted files have extension .txt and stored in different folders
