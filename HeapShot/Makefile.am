
CSC = gmcs -debug
CSFLAGS = -target:exe

TARGET = HeapShot.exe
WRAPPER = heap-shot

CSFILES = 			\
	HeapShot.cs		\
	ReferenceTreeReport.cs

bin_SCRIPTS =			\
	$(WRAPPER)

SRCDIR_CSFILES = $(CSFILES:%=$(srcdir)/%)

$(TARGET): $(SRCDIR_CSFILES)
	$(CSC) -out:$@ -r:../HeapShot.Reader/HeapShot.Reader.dll $(CSFLAGS) $^

all: $(TARGET) $(WRAPPER)

install-data-local: $(TARGET)
	$(mkinstalldirs) $(DESTDIR)$(pkglibdir)
	$(INSTALL_DATA) $(TARGET) $(DESTDIR)$(pkglibdir)

uninstall-local:
	cd $(DESTDIR)$(pkglibdir) && rm -f $(TARGET)


EXTRA_DIST = 			\
	$(CSFILES)		\
	$(WRAPPER).in

CLEANFILES =			\
	$(TARGET)		\
	$(TARGET).mdb		\
	$(WRAPPER)
